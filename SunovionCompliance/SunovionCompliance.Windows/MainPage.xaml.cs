using DT.GoogleAnalytics.Metro;
using SQLite;
using SunovionCompliance.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Popups;
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
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public List<CategoryType> categories { get; set; }
        public List<PdfInfo> documents { get; set; }
        public int? LastSelectedIndex;
        public LinearGradientBrush CategoryBackgroundBrush;
        
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

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            MessageDialog test;
            String returnValue = await UpDatabase();
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
                foreach (PdfInfo newPdfInfo in backendPdfData)
                {
                    if (devicePdfDataQuery.Where(item => item.DocumentName == newPdfInfo.DocumentName).Count() == 0 )
                    {
                        newPdfInfo.DocumentName = newPdfInfo.DocumentName.Trim();
                        var rowAdded = await conn.InsertAsync(newPdfInfo);
                    }
                    else
                    {
                        PdfInfo tempItem = newPdfInfo;
                        tempItem.Id = devicePdfDataQuery.Where(item => item.DocumentName == newPdfInfo.DocumentName).First<PdfInfo>().Id;
                        tempItem.Keyword1 = newPdfInfo.Keyword1.Trim();
                        await conn.UpdateAsync(tempItem);
                    }
                }

                //test = new MessageDialog(newItem.Category1 );
                //await test.ShowAsync();

                var query2 = conn.Table<CategoryType>();
                var query3 = conn.Table<PdfInfo>();
                categories = await query2.ToListAsync();
                documents = await query3.ToListAsync();

                foreach (CategoryType item in categories)
                {
                    item.Category = item.Category.ToUpper();
                }
                foreach (PdfInfo item in documents)
                {
                    item.Revision = "Year: " + System.DateTime.Parse(item.RevisionDate).Year + " Revision " + item.Revision;
                }

                CategoryList.ItemsSource = categories;
                //DocumentList.ItemsSource = documents;
                //UpdateList.ItemsSource = documents;
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
                ShowPdf(item);
            }
        }

        // Launch the URI
        async void ShowPdf(PdfInfo Item)
        {
            
            if(Item.FileLocation == null){
                StorageFile databaseFile = await Package.Current.InstalledLocation.GetFileAsync("Assets\\CompliancePdfs\\" + Item.DocumentName + ".pdf");
                await Windows.System.Launcher.LaunchFileAsync(databaseFile);
            }
            else
            {
                StorageFile databaseFile = await Package.Current.InstalledLocation.GetFileAsync("Assets\\CompliancePdfs\\" + Item.FileLocation + ".pdf");
                await Windows.System.Launcher.LaunchFileAsync(databaseFile);
            }

            // Create a Uri object from a URI string 
            string uriToLaunch = @"http://www.bing.com";
            var uri = new Uri(uriToLaunch);
            // Launch the URI
            //var success = await Windows.System.Launcher.LaunchUriAsync(uri);
        }

        async private void CategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var SelectedIndex = CategoryList.SelectedIndex;
            CategoryType categortySelected = e.AddedItems[0] as CategoryType;

            SQLiteAsyncConnection conn = new SQLiteAsyncConnection("ComplianceDb.db");
            AsyncTableQuery<PdfInfo> relatedItemsQuery = conn.Table<PdfInfo>();
            relatedItemsQuery = relatedItemsQuery.Where(fi => fi.Category1.ToUpper().Contains(categortySelected.Category));            
            documents = await relatedItemsQuery.ToListAsync();
            if (documents.Count != 0)
            {
                foreach (PdfInfo item in documents)
                {
                    item.Revision = "Year: " + System.DateTime.Parse(item.RevisionDate).Year + " Revision " + item.Revision;
                }
                DocumentList.ItemsSource = documents;
            }
            else
            {
                DocumentList.ItemsSource = null;
            }

            if (SelectedIndex != null)
            {
                Color color = Color.FromArgb(255,Convert.ToByte("cb", 16),Convert.ToByte("cb", 16),Convert.ToByte("cb", 16) );
                ((ListViewItem)CategoryList.ContainerFromIndex(SelectedIndex)).Background = new SolidColorBrush(color);

                //StackPanel SelectedItemStackpanel = (StackPanel) ((ListViewItem)CategoryList.ContainerFromIndex(SelectedIndex)).Content;
                //((Image)SelectedItemStackpanel.Children[0]).Source = new BitmapImage( new Uri( Package.Current.InstalledLocation.Path + @"\Assets\CategoryIconSelected.png") );


                //var messageDialog = new MessageDialog( ((ListViewItem)CategoryList.ContainerFromIndex(SelectedIndex)).Template.GetValue.ToString() );
                //await messageDialog.ShowAsync();
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
            documents = await conn.Table<PdfInfo>().Where(item => item.Keyword1 == queryString).ToListAsync();
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
    }
}
