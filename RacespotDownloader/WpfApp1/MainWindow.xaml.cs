using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Transfer;
using Config.Net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        static HttpClient client = new HttpClient();
        IMySettings settings;

        List<Series> series;
        TransferUtility transferUtility;
        public MainWindow()
        {
            InitializeComponent();
            progressBar.Visibility = Visibility.Hidden;

            settings = new ConfigurationBuilder<IMySettings>()
               .UseJsonFile("settings.json")
               .Build();
            if(!String.IsNullOrEmpty(settings.UserPath))
            {
                pathLabelValue.Content = settings.UserPath;
                downloadButton.IsEnabled = true;
            }
            series = new List<Series>();
            cmbSeries.ItemsSource = new ObservableCollection<Series>();

            var credentials = new BasicAWSCredentials(settings.AWSAccessKey, settings.AWSSecretKey);
            var s3Client = new AmazonS3Client(credentials, RegionEndpoint.GetBySystemName("us-east-2"));
            var config = new TransferUtilityConfig();
            config.ConcurrentServiceRequests = 10;
            transferUtility = new TransferUtility(s3Client, config);
        }

        async Task<List<Series>> GetSeriesListAsync()
        {
            List<Series> series = null;
            HttpResponseMessage response = await client.GetAsync("https://api.racespot.media/series");
            if (response.IsSuccessStatusCode)
            {
                series = JsonConvert.DeserializeObject<List<Series>>(await response.Content.ReadAsStringAsync());
                this.series = series;
            }
            return series;
        }

        async Task<List<Livery>> GetLiveriesListAsync(Guid seriesId)
        {
            List<Livery> liveries = null;
            HttpResponseMessage response = await client.GetAsync($"https://api.racespot.media/series/{seriesId}/liveries/download");
            if (response.IsSuccessStatusCode)
            {
                liveries = JsonConvert.DeserializeObject<List<Livery>>(await response.Content.ReadAsStringAsync());
            }
            return liveries;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            cmbSeries.ItemsSource = await GetSeriesListAsync();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();

                if(result == System.Windows.Forms.DialogResult.OK)
                {
                    var path = dialog.SelectedPath;
                    settings.UserPath = path;
                    downloadButton.IsEnabled = true;
                }
                else
                {
                    settings.UserPath = "";
                    downloadButton.IsEnabled = false;
                }
                pathLabelValue.Content = settings.UserPath;
            }
        }

        private async void downloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (cmbSeries.SelectedIndex == -1)
            {
                return;
            }

            var selectedSeries = series[cmbSeries.SelectedIndex];
            var liveries = await GetLiveriesListAsync(selectedSeries.Id);

            var filePaths = liveries.Select(l => GetFileName(l, selectedSeries)).ToList();
            Console.Write(liveries.Count);
        }
        
        private string GetFileName(Livery livery, Series series)
        {
            string id = livery.IsTeam() ? livery.ITeamId : livery.IracingId;
            string itemPath;
            string fileType = "tga";
            string carNumPath = series.IsLeague && livery.IsCustomNumber ? "_num" : "";
            string teamPath = livery.IsTeam() ? "_team" : "";
            switch (livery.LiveryType)
            {
                case LiveryType.Helmet:
                    itemPath = "helmet";
                    break;
                case LiveryType.Suit:
                    itemPath = "suit";
                    break;
                case LiveryType.SpecMap:
                    itemPath = "car_spec";
                    fileType = "mip";
                    break;
                case LiveryType.Car:
                default:
                    itemPath = $"car{carNumPath}";
                    break;
            }

            if (livery.LiveryType == LiveryType.Car || livery.LiveryType == LiveryType.SpecMap)
            {
                return $"{series.Id}/{livery.carPath}/{itemPath}{teamPath}_{id}.{fileType}";
            } else
            {
                return $"{series.Id}/{itemPath}{teamPath}_{id}.{fileType}";
            }
        }
    }
}
