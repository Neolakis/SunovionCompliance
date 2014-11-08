using SQLite;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace SunovionCompliance.Model
{

    [Table("Categories")]
    public class CategoryType
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Category { get; set; }
    }

    public class CmsAnnouncementWrapper
    {
        string message { get; set; }
        string success { get; set; }
        public List<Announcement> data { get; set; }
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
        public List<string> keywords { get; set; }
    }

    [Table("CompliancePdfs")]
    public class PdfInfo
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int CmsId { get; set; }
        public string Category1 { get; set; }
        public string Category2 { get; set; }
        public string DocumentName { get; set; }
        public string Revision { get; set; }
        public string RevisionDate { get; set; }
        public string ShortDescription { get; set; }
        public string FileLocation { get; set; }
        public string Type { get; set; }
        public bool Favorite { get; set; }
        public bool Updated { get; set; }
        public string mimeType { get; set; }
        public string lastModified { get; set; }

        [Ignore]
        public string RevisionPlusDate { get; set; }
        [Ignore]
        public string TitlePlusNew { get; set; }
    }

    [Table("Announcements")]
    public class Announcement
    {
        [PrimaryKey, AutoIncrement]
        public int id { get; set; }
        public string title { get; set; }
        public string body { get; set; }
        public string created { get; set; }

        [Ignore]
        public DateTime sortingDate { get; set; }
    }
    [Table("Keywords")]
    public class Keyword
    {
        [PrimaryKey, AutoIncrement]
        public int id { get; set; }
        public int cmsId { get; set; }
        public string keyword { get; set; }
    }
    public static class Helper
    {
        public static PdfInfo convertCmsPdfToApp(CmsPdf cmsItem)
        {
            PdfInfo newPdfInfo = new PdfInfo();
            DateTime dateValue;

            newPdfInfo.CmsId = cmsItem.id;
            newPdfInfo.Category1 = cmsItem.category1;
            newPdfInfo.Category2 = cmsItem.category2;
            newPdfInfo.DocumentName = cmsItem.documentName;
            newPdfInfo.Revision = (cmsItem.revision != null && cmsItem.revision != "" ? cmsItem.revision : "1.0");
            newPdfInfo.RevisionDate = (DateTime.TryParse(cmsItem.revisionDate, out dateValue) ? cmsItem.revisionDate : "1/1/2000");
            newPdfInfo.ShortDescription = (cmsItem.shortDescription != null && cmsItem.shortDescription != "" ? cmsItem.shortDescription : "No Description.");
            newPdfInfo.FileLocation = (cmsItem.fileLocation != null && cmsItem.fileLocation != "" ? cmsItem.fileLocation : null);
            newPdfInfo.Type = (cmsItem.type != null && cmsItem.type != "" ? cmsItem.type : null);
            newPdfInfo.Favorite = false;
            newPdfInfo.mimeType = (cmsItem.mimeType != null && cmsItem.mimeType != "" ? cmsItem.mimeType : null);
            newPdfInfo.lastModified = (DateTime.TryParse(cmsItem.lastModified, out dateValue) ? cmsItem.lastModified : "1/1/2000");

            return newPdfInfo;
        }
    }
}


