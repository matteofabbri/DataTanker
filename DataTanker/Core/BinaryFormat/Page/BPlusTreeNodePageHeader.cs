namespace DataTanker.BinaryFormat.Page
{
    using System;

    using PageManagement;

    internal class BPlusTreeNodePageHeader : PageHeaderBase
    {
        public long ParentPageIndex { get; set; }
        public long PreviousPageIndex { get; set; }
        public long NextPageIndex { get; set; }
        public bool IsLeaf { get; set; }

        public override SizeClass SizeClass
        {
            get { return base.SizeClass; }
            set
            {
                if (value == SizeClass.NotApplicable ||
                    value == SizeClass.MultiPage)
                    throw new InvalidOperationException("Invalid SizeClass."); 

                base.SizeClass = value;
            }
        }

        public override void Read(IPage page)
        {
            base.Read(page);

            ParentPageIndex = BitConverter.ToInt64(page.Content, OnPageOffsets.BPlusTreeNode.ParentPageIndex);
            PreviousPageIndex = BitConverter.ToInt64(page.Content, OnPageOffsets.BPlusTreeNode.PreviousPageIndex);
            NextPageIndex = BitConverter.ToInt64(page.Content, OnPageOffsets.BPlusTreeNode.NextPageIndex);
            IsLeaf = BitConverter.ToBoolean(page.Content, OnPageOffsets.BPlusTreeNode.IsLeaf);
        }

        protected override PageType GetActualPageType()
        {
            return PageType.BPlusTree;
        }

        public short DefaultSize
        {
            get { return 30; }
        }

        public override void WriteToPage(IPage page)
        {
            Length = DefaultSize;
            CheckSizeClass(page.Content.Length);

            base.WriteToPage(page);

            // parent page index
            byte[] spiBytes = BitConverter.GetBytes(ParentPageIndex);
            spiBytes.CopyTo(page.Content, OnPageOffsets.BPlusTreeNode.ParentPageIndex);

            // previous page index
            byte[] ppiBytes = BitConverter.GetBytes(PreviousPageIndex);
            ppiBytes.CopyTo(page.Content, OnPageOffsets.BPlusTreeNode.PreviousPageIndex);

            // next page index
            byte[] npiBytes = BitConverter.GetBytes(NextPageIndex);
            npiBytes.CopyTo(page.Content, OnPageOffsets.BPlusTreeNode.NextPageIndex);

            // is leaf
            byte[] ilBytes = BitConverter.GetBytes(IsLeaf);
            ilBytes.CopyTo(page.Content, OnPageOffsets.BPlusTreeNode.IsLeaf);
        }

        public BPlusTreeNodePageHeader()
        {
            base.SizeClass = SizeClass.Class0;
        }
    }
}