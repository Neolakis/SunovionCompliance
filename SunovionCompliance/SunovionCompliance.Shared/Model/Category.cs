using SQLite;
using System.Collections.Generic;

namespace SunovionCompliance.Model
{

    [Table("Categories")]
    public class CategoryType
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Category { get; set; }
    }

    public class CmsDocumentWrapper
    {
        string message { get; set; }
        string success { get; set; }
        public List<CmsPdf> data { get; set; }
    }
    public class CmsPdf{
        public int id { get; set; }

        public string category1 { get; set; }
        public string category2 { get; set; }
        public string documentName { get; set; }
        public string revision { get; set; }
        public string revisionDate { get; set; }
        public string shortDescription { get; set; }
        public string fileLocation { get; set; }
        public string type { get; set; }
        public string mimeType { get; set; }
        public string lastModified { get; set; }
    }

    [Table("CompliancePdfs")]
    public class PdfInfo
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Category1 { get; set; }
        public string Category2 { get; set; }
        public string DocumentName { get; set; }
        public string Revision { get; set; }
        public string RevisionDate { get; set; }
        public string ShortDescription { get; set; }
        public string FileLocation { get; set; }
        public string Type { get; set; }
        public string Keyword1 { get; set; }
        public bool Favorite { get; set; }
        public bool Updated { get; set; }

        [Ignore]
        public string RevisionPlusDate { get; set; }
        [Ignore]
        public string TitlePlusNew { get; set; }
    }
    
    public class Announcement
    {
        public string Title { get; set; }
        public string Body { get; set; }
        public string Date { get; set; }
    }
}


