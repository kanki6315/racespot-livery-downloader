using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Transfer;
using Config.Net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Amazon.S3.Model;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        static HttpClient _client = new HttpClient();
        private IMySettings _settings;

        private AmazonS3Client _s3Client;
        private List<Series> _series;
        public MainWindow()
        {
            InitializeComponent();
            progressBar.Visibility = Visibility.Hidden;

            _settings = new ConfigurationBuilder<IMySettings>()
               .UseJsonFile("settings.json")
               .Build();
            if(!String.IsNullOrEmpty(_settings.UserPath))
            {
                pathLabelValue.Content = _settings.UserPath;
                downloadButton.IsEnabled = true;
            }
            _series = new List<Series>();
            cmbSeries.ItemsSource = new ObservableCollection<Series>();

            var credentials = new BasicAWSCredentials(_settings.AWSAccessKey, _settings.AWSSecretKey);
            _s3Client = new AmazonS3Client(credentials, RegionEndpoint.GetBySystemName("us-east-2")); 
        }

        async Task<List<Series>> GetSeriesListAsync()
        {
            List<Series> series = null;
            HttpResponseMessage response = await _client.GetAsync("https://api.racespot.media/series");
            if (response.IsSuccessStatusCode)
            {
                series = JsonConvert.DeserializeObject<List<Series>>(await response.Content.ReadAsStringAsync());
                this._series = series;
            }
            return series;
        }

        async Task<List<Livery>> GetLiveriesListAsync(Guid seriesId)
        {
            List<Livery> liveries = null;
            HttpResponseMessage response = await _client.GetAsync($"https://api.racespot.media/series/{seriesId}/liveries/download");
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

        private void path_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();

                if(result == System.Windows.Forms.DialogResult.OK)
                {
                    var path = dialog.SelectedPath;
                    _settings.UserPath = path;
                    downloadButton.IsEnabled = true;
                }
                else
                {
                    _settings.UserPath = "";
                    downloadButton.IsEnabled = false;
                }
                pathLabelValue.Content = _settings.UserPath;
            }
        }

        private async void downloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (cmbSeries.SelectedIndex == -1)
            {
                return;
            }

            progressBar.Value = 0;
            progressBar.Visibility = Visibility.Visible;
            pathButton.IsEnabled = false;
            downloadButton.IsEnabled = false;

            var selectedSeries = _series[cmbSeries.SelectedIndex];
            var liveries = await GetLiveriesListAsync(selectedSeries.Id);
            
            var progress = new Progress<double>(value => { progressBar.Value = value; });
            await Task.Run(() => DownloadLiveries(liveries, selectedSeries, progress));
            //await DownloadLiveries(liveries, selectedSeries, progress);
            progressBar.Visibility = Visibility.Hidden;
            pathButton.IsEnabled = true;
            downloadButton.IsEnabled = true;
        }

        
        public async Task DownloadLiveries(IList<Livery> liveries, Series series, IProgress<double> progress)
        {
            if (liveries.Count == 0)
                return;

        
            var tasks = new List<Task>();
            for (int i = 0; i < liveries.Count; i++)
            {
                
                tasks.Add(GetS3Object(GetS3FilePath(liveries[i], series), GetLocalFileName(liveries[i], series)));
                

                if (tasks.Count == 20 || i == liveries.Count - 1)
                {
                    await Task.WhenAll(tasks);
                    tasks.Clear();
                }
                
                progress?.Report((double) i / liveries.Count * 100);
            }                        
        }
        
        private string GetLocalFileName(Livery livery, Series series)
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
                return $"{_settings.UserPath}\\{livery.carPath}\\{itemPath}{teamPath}_{id}.{fileType}";
            }
            return $"{_settings.UserPath}\\{itemPath}{teamPath}_{id}.{fileType}";
        }
        
        private string GetS3FilePath(Livery livery, Series series)
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
            }
            return $"{series.Id}/{itemPath}{teamPath}_{id}.{fileType}";
        }

        private async Task GetS3Object(string s3Path, string localPath)
        {
            GetObjectRequest request = new GetObjectRequest()
            {
                BucketName = _settings.BucketName,
                Key = s3Path
            };

            using (var getObjectResponse = await _s3Client.GetObjectAsync(request))
            {            
                await getObjectResponse.WriteResponseStreamToFileAsync(localPath, false, CancellationToken.None);
            }
        }   
    }
}
