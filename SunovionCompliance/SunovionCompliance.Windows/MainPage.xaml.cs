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
using Windows.Storage;
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
        public Uri CmsURL = new Uri("http://webserv.hwpnj.com:8009/");
        //public Uri CmsURL = new Uri("http://localhost:3000/");
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
                    return "Failed to Copy File!";
                }
            }
            return "Unexpected result.";
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {

        }

        protected async Task<string> GetSessionCookie()
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
        protected async Task<string> UpdateDataFromCMS2(){
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
                            foreach (CmsPdf cmsItem in cmsDocWrapper.data)
                            {
                                SunovionCompliance.Model.PdfInfo newPdfInfo = new PdfInfo();
                                if (cmsItem.documentName != null && cmsItem.category1 != null )
                                {
                                    newPdfInfo = SunovionCompliance.Model.Helper.convertCmsPdfToApp(cmsItem);

                                    if (devicePdfDataQuery.Where(item => item.CmsId == newPdfInfo.CmsId).Count() == 0)
                                    {
                                        newPdfInfo.DocumentName = newPdfInfo.DocumentName.Trim();
                                        //newPdfInfo.Updated = true;
                                        var rowAdded = await conn.InsertAsync(newPdfInfo);
                                    }
                                    else
                                    {
                                        PdfInfo tempItem = devicePdfDataQuery.Where(item => item.CmsId == newPdfInfo.CmsId).First<PdfInfo>();
                                        if ( DateTime.Parse(tempItem.lastModified).CompareTo(DateTime.Parse(newPdfInfo.lastModified)) < 0)
                                        {
                                            newPdfInfo.Id = tempItem.Id;
                                            newPdfInfo.Favorite = tempItem.Favorite;
                                            newPdfInfo.Updated = tempItem.Updated;
                                            newPdfInfo.Updated = true;
                                            tempItem = newPdfInfo;
                                            await conn.UpdateAsync(tempItem);
                                            
                                            Uri fileUri = new Uri(@"http://webserv.hwpnj.com:8009/document/" + newPdfInfo.CmsId + @"/data");
                                            StorageFolder localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
                                            StorageFolder newFolder = await localFolder.CreateFolderAsync("CmsFiles", CreationCollisionOption.OpenIfExists);
                                            string filename = newPdfInfo.DocumentName + ".pdf";

                                            await SaveAsync(fileUri, newFolder, filename);
                                        }
                                    }
                                }
                                else
                                {
                                }
                            }
                        }
                        catch(Exception e)
                        {
                            return data;
                        }
                        return data;
                    }
                }
            }
            return "ASDF";
        }

        protected async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            MessageDialog test;
            String returnValue = await UpDatabase();
            test = new MessageDialog(returnValue);
            //await test.ShowAsync();

            returnValue = await GetSessionCookie();
            test = new MessageDialog(returnValue);
            //await test.ShowAsync();
            returnValue = await UpdateDataFromCMS2();
            test = new MessageDialog(returnValue);
            //await test.ShowAsync();


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
                announcements = new List<Announcement>();
                announcements.Add(new Announcement() {
                    Title = "Nam ac risus ut turpis laoreet dignissim vitae vel urna.",
                    Body = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.",
                    Date = "June 9, 2013"
                });

                foreach (CategoryType item in categories)
                {
                    item.Category = item.Category.ToUpper();
                }
                foreach (PdfInfo item in documents)
                {
                    item.TitlePlusNew = item.DocumentName;
                    item.RevisionPlusDate = "Year: " + System.DateTime.Parse(item.RevisionDate).ToString("MM/dd/yyyy") + " Revision " + item.Revision;
                }
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
        
        private void DocumentList_ItemClick(object sender, ItemClickEventArgs e)
        {
            PdfInfo item = e.ClickedItem as PdfInfo;
            if(item != null)
            {
                ShowPdf(item, sender);
            }
            
        }

        // Launch the URI
        async void ShowPdf(PdfInfo Item, object sender)
        {
            MessageDialog test = new MessageDialog("ASDF");
            //await test.ShowAsync();

            SQLiteAsyncConnection conn = new SQLiteAsyncConnection("ComplianceDb.db");
            int primaryKey = Item.Id;
            Item.Updated = false;
            await conn.UpdateAsync(Item);

            var query3 = conn.Table<PdfInfo>();
            List<PdfInfo> tempList = await query3.ToListAsync();
            UpdatedList.ItemsSource = tempList.Where(info => info.Updated == true).ToList();
            documents.Where(doc => doc.Id == primaryKey).First().Updated = false;

            if (Item.DocumentName != null)
            {
                StorageFolder localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
                StorageFolder newFolder = await localFolder.CreateFolderAsync("CmsFiles", CreationCollisionOption.OpenIfExists);
                StorageFile databaseFile = await newFolder.GetFileAsync("Assets\\CmsFiles\\" + Item.DocumentName + ".pdf");
                await Windows.System.Launcher.LaunchFileAsync(databaseFile);
            }
        }

        public async static Task<StorageFile> SaveAsync(
        Uri fileUri,
        StorageFolder folder,
        string fileName)
        {
            var file = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
            var downloader = new BackgroundDownloader();
            var download = downloader.CreateDownload(
                fileUri,
                file);

            var res = await download.StartAsync();

            return file;
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
                    item.RevisionPlusDate = "Year: " + System.DateTime.Parse(item.RevisionDate).ToString("mm/dd/yyyy") + " Revision " + item.Revision;
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
            string queryString = args.QueryText;

            SQLiteAsyncConnection conn = new SQLiteAsyncConnection("ComplianceDb.db");
            documents = new ObservableCollection<PdfInfo>(await conn.Table<PdfInfo>().Where(item => item.Keyword1 == queryString).ToListAsync());
            List<PdfInfo> tempList = await conn.Table<PdfInfo>().ToListAsync();
            foreach (PdfInfo tempItem in tempList)
            {
                string wildCardQuery = tempItem.Keyword1;
                if (tempItem.Keyword1.Contains('%') && queryString.Contains(wildCardQuery.Trim('%')))
                {
                    documents.Add(tempItem);
                }
            }
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
                tempItem.RevisionPlusDate = "Year: " + System.DateTime.Parse(tempItem.RevisionDate).ToString("mm/ddyyy") + " Revision " + tempItem.Revision;
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
            //Clarinet.Play();

            // Create a string with the tile template xml.
            // Note that the version is set to "3" and that fallbacks are provided for the Square150x150 and Wide310x150 tile sizes.
            // This is so that the notification can be understood by Windows 8 and Windows 8.1 machines as well.
            string message = "Last update was at " + DateTime.Now.ToString("h:mm tt") + Environment.NewLine + "0 Updates were downloaded."; 
            string tileXmlString =
                "<tile>"
                + "<visual version='3'>"
                + "<binding template='TileSquare150x150Text04' fallback='TileSquareText04'>"
                + "<text id='1'>"+message+"</text>"
                + "</binding>"
                + "<binding template='TileWide310x150Text03' fallback='TileWideText03'>"
                + "<text id='1'>" + message + "</text>"
                + "</binding>"
                + "<binding template='TileSquare310x310Text09'>"
                + "<text id='1'>" + message + "</text>"
                + "</binding>"
                + "</visual>"
                + "</tile>";

            // Create a DOM.
            Windows.Data.Xml.Dom.XmlDocument tileDOM = new Windows.Data.Xml.Dom.XmlDocument(); 

            // Load the xml string into the DOM.
            tileDOM.LoadXml(tileXmlString);

            // Create a tile notification.
            TileNotification tile = new TileNotification(tileDOM);

            // Send the notification to the application? tile.
            //TileUpdateManager.CreateTileUpdaterForApplication().Update(tile);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {

            //Clarinet.Stop();
            // TileUpdater is also used to clear the tile.
            TileUpdateManager.CreateTileUpdaterForApplication().Clear();
        }

    }
}
