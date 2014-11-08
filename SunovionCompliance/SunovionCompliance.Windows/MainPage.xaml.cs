using DT.GoogleAnalytics.Metro;
using SQLite;
using SunovionCompliance.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Data.Html;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking.BackgroundTransfer;
using Windows.Networking.Connectivity;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Notifications;
using Windows.UI.Popups;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace SunovionCompliance
{
    public class CmsUserClass
    {
        public string username { get; set; }
        public string password { get; set; }
    }
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public List<CategoryType> categories { get; set; }
        public List<Announcement> announcements { get; set; }
        public ObservableCollection<PdfInfo> documents { get; set; }
        public List<PdfInfo> updates { get; set; }
        public List<PdfInfo> favorites { get; set; }
        public string sessionCookie { get; set; }
        public int? LastSelectedIndex;
        public int? CurrentSelectedIndex;
        public LinearGradientBrush CategoryBackgroundBrush;
        //public Uri CmsURL = new Uri("http://webserv.hwpnj.com:8009/");
        //public Uri CmsURL = new Uri("http://ryanday.net:3000/");
        public Uri CmsURL = new Uri("http://localhost:3000/");
        public Uri CmsURL_Production = new Uri("http://webserv.hwpnj.com:8009/");
        
        public MainPage()
        {
            this.InitializeComponent();
            
            LinearGradientBrush gradient = new LinearGradientBrush();
            gradient.StartPoint = new Point( 0.5, 0 );
            gradient.EndPoint = new Point( 0.5, 1 );
            gradient.GradientStops.Add(new GradientStop() { Color = Colors.White, Offset = 0 });
            gradient.GradientStops.Add(new GradientStop() { Color = Color.FromArgb(255, 203, 203, 203), Offset = 1 });
            CategoryBackgroundBrush = gradient;
        }

        private async Task<string> UpDatabase()
        {
            bool isDatabaseExisting = false;
            try
            {
                StorageFile storageFile = await ApplicationData.Current.LocalFolder.GetFileAsync("ComplianceDb.db");
                isDatabaseExisting = true;

                return storageFile.Path;
            }
            catch
            {
                isDatabaseExisting = false;
            }
            if (!isDatabaseExisting)
            {
                StorageFile databaseFile = await Package.Current.InstalledLocation.GetFileAsync("Assets\\ComplianceDb.db");

                try
                {
                    await databaseFile.CopyAsync(ApplicationData.Current.LocalFolder);
                }
                catch (Exception e)
                {
                    return "Failed to Copy File! - " + e.Message;
                }
            }
            return "Unexpected result.";
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {

        }

        public async Task<string> GetSessionCookie()
        {
            Uri url =  new Uri(CmsURL, "login");

            CookieContainer cookieJar = new CookieContainer();
            HttpClientHandler handler = new HttpClientHandler()
            {
                CookieContainer = cookieJar
            };
            cookieJar.Add(CmsURL, new Cookie("sunovionsession2", "cookie_value"));
            handler.UseCookies = true;
            handler.UseDefaultCredentials = false;

            using (var client = new HttpClient(handler))
            {
                CmsUserClass admin = new CmsUserClass {
                    username = "admin",
                    password = "ladeda@"
                };

                //Create a Json Serializer for our type
                DataContractJsonSerializer jsonSer = new DataContractJsonSerializer(typeof(CmsUserClass));

                // use the serializer to write the object to a MemoryStream
                MemoryStream ms = new MemoryStream();
                jsonSer.WriteObject(ms, admin);
                ms.Position = 0;

                //use a Stream reader to construct the StringContent (Json)
                StreamReader sr = new StreamReader(ms);
                StringContent theContent = new StringContent(sr.ReadToEnd(), System.Text.Encoding.UTF8, "application/json");

                client.DefaultRequestHeaders.Connection.Add("keep-alive");
                client.DefaultRequestHeaders.Host = "localhost:3000";
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var response = await client.PostAsync(url, theContent);
                var responseString = await response.Content.ReadAsStringAsync();

                if (response.ToString().Contains("sunovionsession="))
                {
                    String rawResponse = response.ToString();
                    responseString = rawResponse.Substring(rawResponse.IndexOf("sunovionsession=")).Split(';').First();
                    sessionCookie = responseString;
                }
                return responseString;
            }
        }
        public async Task<string> UpdateDocumentsFromCms()
        {
            var handler = new HttpClientHandler { UseCookies = false };
            using (var httpClient = new HttpClient(handler))
            {
                var url = CmsURL + "documents";

                using (var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    //httpRequestMessage.Headers.Add("User-Agent", "Fiddler");
                    httpRequestMessage.Headers.Add("Cookie", sessionCookie);
                    using (var httpResponseMessage = await httpClient.SendAsync(httpRequestMessage))
                    {
                        // do something with the response
                        var data = await httpResponseMessage.Content.ReadAsStringAsync();

                        CmsDocumentWrapper cmsDocWrapper;
                        DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(CmsDocumentWrapper));
                        using (MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes(data)))
                        {
                            cmsDocWrapper = serializer.ReadObject(stream) as CmsDocumentWrapper;
                        }
                        try
                        {
                            SQLiteAsyncConnection conn = new SQLiteAsyncConnection("ComplianceDb.db");
                            List<PdfInfo> devicePdfDataQuery = await conn.Table<PdfInfo>().ToListAsync();
                            List<Keyword> deviceKeywordQuery = await conn.Table<Keyword>().ToListAsync();
                            foreach (Keyword deviceItem in deviceKeywordQuery)
                            {
                                await conn.DeleteAsync(deviceItem);
                            }
                            foreach (PdfInfo deviceItem in devicePdfDataQuery)
                            {
                                if (cmsDocWrapper.data.Where(item => item.id == deviceItem.CmsId).Count() == 0)
                                {
                                    await conn.DeleteAsync(deviceItem);
                                }
                            }
                            foreach (CmsPdf cmsItem in cmsDocWrapper.data)
                            {
                                SunovionCompliance.Model.PdfInfo newPdfInfo = new PdfInfo();
                                if (cmsItem.documentName != null && cmsItem.category1 != null)
                                {
                                    newPdfInfo = SunovionCompliance.Model.Helper.convertCmsPdfToApp(cmsItem);

                                    if (devicePdfDataQuery.Where(item => item.CmsId == newPdfInfo.CmsId).Count() == 0)
                                    {
                                        newPdfInfo.DocumentName = newPdfInfo.DocumentName.Trim();
                                        newPdfInfo.Updated = true;
                                        var rowAdded = await conn.InsertAsync(newPdfInfo);

                                        saveCmsFile(newPdfInfo);
                                    }
                                    else
                                    {
                                        PdfInfo tempItem = devicePdfDataQuery.Where(item => item.CmsId == newPdfInfo.CmsId).First<PdfInfo>();
                                        if (DateTime.Parse(tempItem.lastModified).CompareTo(DateTime.Parse(newPdfInfo.lastModified)) < 0)
                                        {
                                            newPdfInfo.Id = tempItem.Id;
                                            newPdfInfo.Favorite = tempItem.Favorite;
                                            newPdfInfo.Updated = true;
                                            tempItem = newPdfInfo;
                                            await conn.UpdateAsync(tempItem);

                                            saveCmsFile(newPdfInfo);
                                        }
                                    }

                                    foreach (string keywordItem in cmsItem.keywords)
                                    {
                                        Keyword newKeyword = new Keyword(){
                                            cmsId = cmsItem.id,
                                            keyword = keywordItem
                                        };
                                        await conn.InsertAsync(newKeyword);
                                    }
                                }
                                else
                                {
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            return data + e.Message;
                        }
                        return data;
                    }
                }
            }
        }
        public async Task<string> UpdateAnnouncementsFromCms()
        {
            var handler = new HttpClientHandler { UseCookies = false };
            using (var httpClient = new HttpClient(handler))
            {
                var url = CmsURL + "announcements";

                using (var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    httpRequestMessage.Headers.Add("Cookie", sessionCookie);
                    using (var httpResponseMessage = await httpClient.SendAsync(httpRequestMessage))
                    {
                        // do something with the response
                        var data = await httpResponseMessage.Content.ReadAsStringAsync();

                        CmsAnnouncementWrapper cmsAnnounceWrapper;
                        DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(CmsAnnouncementWrapper));
                        using (MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes(data)))
                        {
                            cmsAnnounceWrapper = serializer.ReadObject(stream) as CmsAnnouncementWrapper;
                        }

                        SQLiteAsyncConnection conn = new SQLiteAsyncConnection("ComplianceDb.db");
                        List<Announcement> deviceAnnouncementsQuery = await conn.Table<Announcement>().ToListAsync();
                        foreach (Announcement deviceItem in deviceAnnouncementsQuery)
                        {
                            await conn.DeleteAsync(deviceItem);
                        }
                        announcements = new List<Announcement>();
                        foreach (Announcement item in cmsAnnounceWrapper.data)
                        {
                            DateTime formattedDate = new DateTime();
                            if (DateTime.TryParse(item.created, out formattedDate))
                            {
                                item.created = formattedDate.ToString("MMM dd, yyyy");
                                item.sortingDate = formattedDate;
                            }
                            else
                            {
                                item.created = "";
                                item.sortingDate = new DateTime(1999, 12, 12);
                            }
                            announcements.Add(item);
                        }
                        announcements = announcements.OrderByDescending(news => news.sortingDate).ToList();
                        await conn.InsertAllAsync(announcements);

                        return data;
                    }
                }
            }
        }

        private async void saveCmsFile(PdfInfo newPdfInfo)
        {
            if (newPdfInfo.mimeType != null && newPdfInfo.mimeType.Equals("application/pdf") )
            {
                Uri fileUri = new Uri(CmsURL + "document/" + newPdfInfo.CmsId + @"/data");
                StorageFolder localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
                StorageFolder newFolder = await localFolder.CreateFolderAsync("CmsFiles", CreationCollisionOption.OpenIfExists);
                string filename = newPdfInfo.DocumentName + ".pdf";

                await SaveAsync(fileUri, newFolder, filename);
            }
        }

        public async void Page_Loaded(object sender, RoutedEventArgs e)
        {            
            String returnValue = "Use this for debugging.";
            returnValue = "Am I connected to the internet? " + IsConnectedToInternet();
            MessageDialog test = new MessageDialog(returnValue);
            //await test.ShowAsync();
            
            // Initialize Database on first open
            returnValue = await UpDatabase();

            // Update data from CMS
            if (IsConnectedToInternet())
            {
                returnValue = await GetSessionCookie();
                returnValue = await UpdateDocumentsFromCms();
                returnValue = await UpdateAnnouncementsFromCms();
            }
            else
            {
                test = new MessageDialog("Not connected to the internet, unable to update list of Compliance PDFs.");
                await test.ShowAsync();
            }
            await UpdateLiveTile();

            List<PdfInfo> backendPdfData;
            // acquire file
            var _File = await Package.Current.InstalledLocation.GetFileAsync("Assets\\simplePdfInfo.json");
            // read content
            string responseText = await Windows.Storage.FileIO.ReadTextAsync(_File);
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<PdfInfo>));
            using (MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes(responseText)))
            {
                backendPdfData = serializer.ReadObject(stream) as List<PdfInfo>;
            }
            
            try
            {
                SQLiteAsyncConnection conn = new SQLiteAsyncConnection("ComplianceDb.db");                
                List<PdfInfo> devicePdfDataQuery = await conn.Table<PdfInfo>().ToListAsync();
                //foreach (PdfInfo newPdfInfo in backendPdfData)
                //{
                //    newPdfInfo.Favorite = false;
                //    if (devicePdfDataQuery.Where(item => item.DocumentName == newPdfInfo.DocumentName).Count() == 0)
                //    {
                //        newPdfInfo.DocumentName = newPdfInfo.DocumentName.Trim();
                //        var rowAdded = await conn.InsertAsync(newPdfInfo);
                //    }
                //    else
                //    {
                //        PdfInfo tempItem = newPdfInfo;
                //        tempItem.Id = devicePdfDataQuery.Where(item => item.DocumentName == newPdfInfo.DocumentName).First<PdfInfo>().Id;
                //        tempItem.Keyword1 = newPdfInfo.Keyword1.Trim();
                //        await conn.UpdateAsync(tempItem);
                //    }
                //}

                //test = new MessageDialog(newItem.Category1 );
                //await test.ShowAsync();

                var query2 = conn.Table<CategoryType>();
                var query3 = conn.Table<PdfInfo>();
                categories = await query2.OrderBy(category => category.Category).ToListAsync();
                documents = new ObservableCollection<PdfInfo>(await query3.OrderBy(info => info.DocumentName).ToListAsync());

                foreach (CategoryType item in categories)
                {
                    item.Category = item.Category.ToUpper();
                }
                formatDocumentList();
                // Set updates, favorites after modification to master document list have been made.
                updates = documents.Where(info => info.Updated == true).ToList();
                favorites = documents.Where(info => info.Favorite == true).ToList();

                CategoryList.ItemsSource = categories;
                AnnouncementList.ItemsSource = announcements;
                DocumentList.ItemsSource = documents;
                FavoritesList.ItemsSource = favorites;
                UpdatedList.ItemsSource = updates;
            }
            catch (Exception e2)
            {
                test = new MessageDialog(e2.Message);
            }
            
            //await test.ShowAsync();
        }

        private void formatDocumentList()
        {
            foreach (PdfInfo item in documents)
            {
                item.TitlePlusNew = item.DocumentName;
                item.RevisionPlusDate = "Date: " + System.DateTime.Parse(item.RevisionDate).ToString("MM/dd/yyyy") + " Revision " + item.Revision;
            }
        }
        
        private void DocumentList_ItemClick(object sender, ItemClickEventArgs e)
        {
            PdfInfo item = e.ClickedItem as PdfInfo;
            if(item != null && item.DocumentName != null)
            {
                ShowPdf(item, sender);
            }
            
        }

        // Launch the URI
        async void ShowPdf(PdfInfo Item, object sender)
        {
            SQLiteAsyncConnection conn = new SQLiteAsyncConnection("ComplianceDb.db");
            int primaryKey = Item.Id;
            Item.Updated = false;
            await conn.UpdateAsync(Item);

            var query3 = conn.Table<PdfInfo>();
            List<PdfInfo> tempList = await query3.ToListAsync();
            UpdatedList.ItemsSource = tempList.Where(info => info.Updated == true).ToList();
            documents.Where(doc => doc.Id == primaryKey).First().Updated = false;

            FileNotFoundException exception = null; 
            try
            {
                StorageFolder localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
                StorageFolder newFolder = await localFolder.CreateFolderAsync("CmsFiles", CreationCollisionOption.OpenIfExists);
                StorageFile databaseFile = await newFolder.GetFileAsync(Item.DocumentName + ".pdf");
                await Windows.System.Launcher.LaunchFileAsync(databaseFile);
            }
            catch (FileNotFoundException notFound)
            {
                exception = notFound;
            }

            if (exception != null)
                await new MessageDialog("There is no file associated with this entry.").ShowAsync();
        }

        public async Task SaveAsync(Uri fileUri, StorageFolder folder, string fileName)
        {            
            var handler = new HttpClientHandler { UseCookies = false };
            using (var httpClient = new HttpClient(handler))
            {
                using (var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, fileUri))
                {
                    //httpRequestMessage.Headers.Add("User-Agent", "Fiddler");
                    httpRequestMessage.Headers.Add("Cookie", sessionCookie);
                    using (var httpResponseMessage = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead))
                    {
                        StorageFile cmsFile = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                        var fs = await cmsFile.OpenAsync(FileAccessMode.ReadWrite);
                        DataWriter writer = new DataWriter(fs.GetOutputStreamAt(0));
                        writer.WriteBytes(await httpResponseMessage.Content.ReadAsByteArrayAsync());
                        await writer.StoreAsync();
                        writer.DetachStream();
                        await fs.FlushAsync();
                    }
                }
            }            
        }

        async private void CategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var SelectedIndex = CategoryList.SelectedIndex;
            CategoryType categortySelected = e.AddedItems[0] as CategoryType;

            SQLiteAsyncConnection conn = new SQLiteAsyncConnection("ComplianceDb.db");
            AsyncTableQuery<PdfInfo> relatedItemsQuery = conn.Table<PdfInfo>();
            relatedItemsQuery = relatedItemsQuery.Where(fi => fi.Category1.ToUpper().Contains(categortySelected.Category)).OrderBy(fi => fi.DocumentName);
            documents = new ObservableCollection<PdfInfo>(await relatedItemsQuery.ToListAsync());
            if (documents.Count != 0)
            {
                ObservableCollection<PdfInfo> tempCollection = new ObservableCollection<PdfInfo>();
                DocumentList.ItemsSource = tempCollection;

                foreach (PdfInfo item in documents)
                {
                    await Task.Delay(50);
                    item.TitlePlusNew = item.DocumentName;
                    item.RevisionPlusDate = "Year: " + System.DateTime.Parse(item.RevisionDate).ToString("MM/dd/yyyy") + " Revision " + item.Revision;
                    tempCollection.Add(item);
                }
            }
            else
            {
                DocumentList.ItemsSource = null;
            }

            if (SelectedIndex != null)
            {
                Color color = Color.FromArgb(255,Convert.ToByte("cb", 16),Convert.ToByte("cb", 16),Convert.ToByte("cb", 16) );
                ((ListViewItem)CategoryList.ContainerFromIndex(SelectedIndex)).Background = new SolidColorBrush(color);
            }
            if (LastSelectedIndex != null)
            {
                ((ListViewItem)CategoryList.ContainerFromIndex((int)LastSelectedIndex)).Background = CategoryBackgroundBrush;
            }
            LastSelectedIndex = SelectedIndex;
        }

        private async void SearchBox_QuerySubmitted(SearchBox sender, SearchBoxQuerySubmittedEventArgs args)
        {
            // User entered search string.
            string queryString = args.QueryText;

            // Grabbing Keywords and Wild card keywords seperately
            SQLiteAsyncConnection conn = new SQLiteAsyncConnection("ComplianceDb.db");
            List<Keyword> keywordList = await conn.Table<Keyword>().Where(item => item.keyword == queryString).ToListAsync();
            List<Keyword> wildcardList = await conn.Table<Keyword>().ToListAsync();
            documents = new ObservableCollection<PdfInfo>();

            foreach (Keyword keywordItem in keywordList)
            {                
                documents.Add( await conn.Table<PdfInfo>().Where(document => document.CmsId == keywordItem.cmsId).FirstAsync() );
            }
            foreach (Keyword keywordItem in wildcardList)
            {
                if (keywordItem.keyword.Contains('%') && queryString.Contains(keywordItem.keyword.Trim('%')))
                    documents.Add(await conn.Table<PdfInfo>().Where(document => document.CmsId == keywordItem.cmsId).FirstAsync());
            }

            formatDocumentList();
            DocumentList.ItemsSource = documents;
        }

        private async void AddOrRemoveFavorite(PdfInfo item, bool addFavorite)
        {
            SQLiteAsyncConnection conn = new SQLiteAsyncConnection("ComplianceDb.db");

            int primaryKey = item.Id;
            item.Favorite = addFavorite;

            await conn.UpdateAsync(item);

            var query3 = conn.Table<PdfInfo>();
            List<PdfInfo> tempList = await query3.ToListAsync();
            tempList = tempList.Where(info => info.Favorite == true).ToList();
            foreach (PdfInfo tempItem in tempList)
            {
                item.TitlePlusNew = item.DocumentName;
                tempItem.RevisionPlusDate = "Date: " + System.DateTime.Parse(tempItem.RevisionDate).ToString("MM/dd/yyyy") + " Revision " + tempItem.Revision;
            }
            FavoritesList.ItemsSource = tempList;
        }

        private void ImageToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            PdfInfo item = (sender as FrameworkElement).DataContext as PdfInfo;

            if (item != null)
            {
                AddOrRemoveFavorite(item, true);
                //FavoritesList.ItemsSource = SunovionCompliance.Model.Helper.AddOrRemoveFavorite2(item, true);
            }

        }

        private void ImageToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            PdfInfo item = (sender as FrameworkElement).DataContext as PdfInfo;

            if (item != null)
            {
                AddOrRemoveFavorite(item, false);
                //FavoritesList.ItemsSource = SunovionCompliance.Model.Helper.AddOrRemoveFavorite2(item, false);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
        }

        private async Task UpdateLiveTile()
        {
            BadgeUpdateManager.CreateBadgeUpdaterForApplication().Clear();
            SQLiteAsyncConnection conn = new SQLiteAsyncConnection("ComplianceDB.db");
            int updateCount = await conn.Table<PdfInfo>().Where(document => document.Updated == true).CountAsync();
            if (updateCount > 0)
                UpdateBadgeWithNumberWithStringManipulation(updateCount);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
        }
        
        public static bool IsConnectedToInternet()
        {
            ConnectionProfile connectionProfile = NetworkInformation.GetInternetConnectionProfile();
            //return (connectionProfile != null && connectionProfile.GetNetworkConnectivityLevel() == NetworkConnectivityLevel.InternetAccess);
            return true;
        }

        void UpdateBadgeWithNumberWithStringManipulation(int number)
        {
            // Create a string with the badge template xml.
            string badgeXmlString = "<badge value='" + number + "'/>";
            Windows.Data.Xml.Dom.XmlDocument badgeDOM = new Windows.Data.Xml.Dom.XmlDocument();
            try
            {
                // Create a DOM.
                badgeDOM.LoadXml(badgeXmlString);

                // Load the xml string into the DOM, catching any invalid xml characters.
                BadgeNotification badge = new BadgeNotification(badgeDOM);

                // Create a badge notification and send it to the application’s tile.
                BadgeUpdateManager.CreateBadgeUpdaterForApplication().Update(badge);

                //OutputTextBlock.Text = badgeDOM.GetXml();
                //rootPage.NotifyUser("Badge sent", NotifyType.StatusMessage);
            }
            catch (Exception)
            {
                //OutputTextBlock.Text = string.Empty;
                //rootPage.NotifyUser("Error loading the xml, check for invalid characters in the input", NotifyType.ErrorMessage);
            }
        }
    }
}
