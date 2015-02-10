﻿namespace DataTanker
{
    using System;
    using System.Diagnostics;
    using System.IO;

    using PageManagement;
    using BinaryFormat.Page;
    using Settings;

    /// <summary>
    /// Represents a data Storage.
    /// </summary>
    internal class Storage : IStorage
    {
        private bool _disposed;

        private readonly IPageManager _pageManager;
        private string _path = string.Empty;
        private bool _isOpen;

        private string _infoFileName = "info";

        private StorageInfo _info;

        public StorageInfo Info
        {
            get { return _info; }
        }

        private string InfoFileName()
        {
            return Path + System.IO.Path.DirectorySeparatorChar + _infoFileName;
        }

        protected virtual void Init()
        {
            // add header page
            IPage headingPage = PageManager.CreatePage();

            Debug.Assert(headingPage.Index == 0, "The header page should have zero index");

            var hph = new HeadingPageHeader
                          {
                              FsmPageIndex = 1, 
                              AccessMethodPageIndex = 2,
                              PageSize = PageManager.PageSize,
                              OnDiskStructureVersion = OnDiskStructureVersion,
                              AccessMethod = (short)AccessMethod
                          };

            PageFormatter.InitPage(headingPage, hph);
            PageManager.UpdatePage(headingPage);

            // add the first free-space-map page
            IPage fsmPage = PageManager.CreatePage();

            Debug.Assert(fsmPage.Index == 1, "The first free-space-map page should have index 1");

            var fsmh = new FreeSpaceMapPageHeader
                           {
                               StartPageIndex = fsmPage.Index, 
                               PreviousPageIndex = -1, 
                               NextPageIndex = -1,
                               BasePageIndex = 0
                           };

            PageFormatter.InitPage(fsmPage, fsmh);
            PageFormatter.SetAllFsmValues(fsmPage, FsmValue.Full);
            PageManager.UpdatePage(fsmPage);
        }

        private void CheckDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException("Storage");
        }

        #region IStorage Members

        /// <summary>
        /// Gets the on-disk structure version.
        /// </summary>
        public int OnDiskStructureVersion { get { return 1; } }

        /// <summary>
        /// Gets the access method implemented by this storage
        /// </summary>
        public virtual AccessMethod AccessMethod { get { return AccessMethod.Undefined; } }

        /// <summary>
        /// Opens an existing Storage.
        /// </summary>
        /// <param name="path">A string containing information about storage location</param>
        public void OpenExisting(string path)
        {
            CheckDisposed();

            if (PageManager == null)
                throw new InvalidOperationException("Page manager is not set");

            if (_isOpen)
                throw new InvalidOperationException("Storage is already open");

            _path = path;

            ReadInfo();
            CheckInfo();
            
            PageManager.OpenExistingPageSpace();
            _isOpen = true;

            IPage headingPage = PageManager.FetchPage(0);
            var header = PageFormatter.GetPageHeader(headingPage);
            var headingHeader = (HeadingPageHeader) header;

            if(headingHeader == null)
                throw new StorageFormatException("Heading page not found");

            if (headingHeader.PageSize != PageSize)
            {
                var pageSize = PageSize;
                _isOpen = false;
                Close();
                throw new StorageFormatException(string.Format("Page size: {0} bytes is set. But pages of the opening storage is {1} bytes length", pageSize, headingHeader.PageSize));
            }

            if(headingHeader.OnDiskStructureVersion != OnDiskStructureVersion)
            {
                _isOpen = false;
                Close();
                throw new NotSupportedException(string.Format("On-disk structure version {0} is not supported.", headingHeader.OnDiskStructureVersion));
            }

            if (headingHeader.AccessMethod != (short) AccessMethod)
            {
                _isOpen = false;
                Close();
                throw new NotSupportedException(string.Format("Access method {0} is not supported by this instance of storage.", headingHeader.AccessMethod));
            }
        }

        private void ReadInfo()
        {
            var infoFileName = InfoFileName();
            if (!File.Exists(infoFileName))
                throw new FileNotFoundException(string.Format("File '{0}' not found", infoFileName));

            var infoString = File.ReadAllText(infoFileName);
            _info = StorageInfo.FromString(infoString);
        }

        protected virtual void CheckInfo()
        {
            if (_info.StorageClrTypeName != GetType().FullName)
                throw new DataTankerException("Mismatch storage type");
        }

        /// <summary>
        /// Opens existing storage or creates a new one.
        /// </summary>
        /// <param name="path">A string containing information about storage location</param>
        public void OpenOrCreate(string path)
        {
            CheckDisposed();

            if (PageManager == null)
                throw new InvalidOperationException("Page manager is not set");

            if (_isOpen)
                throw new InvalidOperationException("Storage is already open");

            _path = path;
            PageManager.Lock();
            try
            {
                if(PageManager.CanCreate())
                    CreateNew(path);
                else
                    OpenExisting(path);
            }
            finally 
            {
                PageManager.Unlock();
            }

        }

        /// <summary>
        /// Creates a new Storage.
        /// </summary>
        /// <param name="path">A string containing information about storage location</param>
        public void CreateNew(string path)
        {
            CheckDisposed();

            if (PageManager == null)
                throw new InvalidOperationException("Page manager is not set");

            if (_isOpen)
                throw new InvalidOperationException("Unable to create starage because this instance is using to operate with the other storage");

            _path = path;
            FillInfo();
            WriteInfo();
            
            PageManager.CreateNewPageSpace();
            _isOpen = true;

            PageManager.Lock();
            try
            {
                Init();
            }
            finally
            {
                PageManager.Unlock();
            }
        }

        private void WriteInfo()
        {
            if (!File.Exists(InfoFileName()))
                File.WriteAllText(InfoFileName(), _info.ToString());
            else throw new DataTankerException("Storage cannot be created here. Files with names matching the names of storage files already exist. Try to call OpenExisting().");
        }

        protected virtual void FillInfo()
        {
            _info = new StorageInfo
            {
                StorageClrTypeName = GetType().FullName
            };
        }

        /// <summary>
        /// Closes the Storage if it is open.  Actually calls Dispose() method.
        /// </summary>
        public void Close()
        {
            Dispose();
            _isOpen = false;
        }

        /// <summary>
        /// Clears buffers for this Storage and causes any buffered data to be written.
        /// </summary>
        public void Flush()
        {
            CheckDisposed();

            if (PageManager != null)
            {
                var cachingPageManager = PageManager as ICachingPageManager;
                if (cachingPageManager != null)
                    cachingPageManager.Flush();
            }
        }

        /// <summary>
        /// Gets a page size in bytes.
        /// Page is a data block that is write and read entirely.
        /// </summary>
        public int PageSize
        {
            get { return PageManager.PageSize; }
        }

        /// <summary>
        /// Gets a Storage location.
        /// </summary>
        public string Path
        {
            get { return _path; }
            internal set { _path = value; }
        }

        /// <summary>
        /// Gets a value indicating whether a Storage is open.
        /// </summary>
        public bool IsOpen
        {
            get { return _isOpen; }
        }

        #endregion

        protected IPageManager PageManager
        {
            get { return _pageManager; }
        }

        /// <summary>
        /// Initializes a new instance of the Storage.
        /// </summary>
        /// <param name="pageManager">The FileSystemPageManager instance</param>
        internal Storage(IPageManager pageManager)
        {
            _pageManager = pageManager;
            pageManager.Storage = this;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if(PageManager != null)
                    {
                        var disposablePageManager = PageManager as IDisposable;
                        if(disposablePageManager != null)
                            disposablePageManager.Dispose();
                    }
                }
                _disposed = true;
            }
        }

        ~Storage()
        {
            Dispose(false);
        }
    }
}