using SQLite;

namespace SunovionCompliance.Model
{

    [Table("Categories")]
    public class CategoryType
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Category { get; set; }
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
    }    
}


