using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using TwitterClient;
using System.Reactive.Linq;
using System.Configuration;
using System.Windows.Threading;
using System.Windows;
using System.Threading.Tasks;
using TwitterClient.MASC;

namespace TwitterWpfClient.ViewModel
{
    /// <summary>
    /// This class contains properties that the main View can data bind to.
    /// <para>
    /// Use the <strong>mvvminpc</strong> snippet to add bindable properties to this ViewModel.
    /// </para>
    /// <para>
    /// You can also use Blend to data bind with the tool's support.
    /// </para>
    /// <para>
    /// See http://www.galasoft.ch/mvvm
    /// </para>
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
		private Brush RunColor = Brushes.Green;
		private Brush StopColor = Brushes.Pink;
		private Brush _currentColor;
		public Brush CurrentColor
		{
			get { return _currentColor; }
			set { Set(() => CurrentColor, ref _currentColor, value); }
		}
		private ObservableCollection<Payload> _allReadingsWithTopic;
		public ObservableCollection<Payload> AllReadingsWithTopic
		{
			get { return _allReadingsWithTopic; }
			set { Set(() => AllReadingsWithTopic, ref _allReadingsWithTopic, value); }
		}

		private bool _clearUnfoundSentiment;
		public bool ClearUnfoundSentiment
		{
			get { return _clearUnfoundSentiment; }
			set { Set(() => ClearUnfoundSentiment, ref _clearUnfoundSentiment, value); }
		}
		private bool _sendExtendedInformation;
		public bool SendExtendedInformation
		{
			get { return _sendExtendedInformation; }
			set { Set(() => SendExtendedInformation, ref _sendExtendedInformation, value); }
		}
		private bool _requireAll;
		public bool RequireAll
		{
			get { return _requireAll; }
			set { Set(() => RequireAll, ref _requireAll, value); }
		}
		private string _oAuthToken;
		public string OAuthToken
		{
			get { return _oAuthToken; }
			set { Set(() => OAuthToken, ref _oAuthToken, value); }
		}
		private string _oAuthTokenSecret;
		public string OAuthTokenSecret
		{
			get { return _oAuthTokenSecret; }
			set { Set(() => OAuthTokenSecret, ref _oAuthTokenSecret, value); }
		}

		private string _oAuthCustomerKey;
		public string OAuthCustomerKey
		{
			get { return _oAuthCustomerKey; }
			set { Set(() => OAuthCustomerKey, ref _oAuthCustomerKey, value); }
		}
		private string _oAuthConsumerSecret;
		public string OAuthConsumerSecret
		{
			get { return _oAuthConsumerSecret; }
			set { Set(() => OAuthConsumerSecret, ref _oAuthConsumerSecret, value); }
		}
		private string _searchGroups;
		public string SearchGroups
		{
			get { return _searchGroups; }
			set { Set(() => SearchGroups, ref _searchGroups, value); }
		}

		private string _eventHubName;
		public string EventHubName
		{
			get { return _eventHubName; }
			set { Set(() => EventHubName, ref _eventHubName, value); }
		}
		private string _eventHubConnectionString;
		public string EventHubConnectionString
		{
			get { return _eventHubConnectionString; }
			set { Set(() => EventHubConnectionString, ref _eventHubConnectionString, value); }
		}

		/// <summary>
		/// Initializes a new instance of the MainViewModel class.
		/// </summary>
		public MainViewModel()
        {
			CurrentColor = StopColor;
			RegisterAggregates();
			AllReadingsWithTopic = new ObservableCollection<Payload>();
			AllReadingsWithTopic.Add(new Payload() { CreatedAt = DateTime.UtcNow,
					Text = "Welcome to the TwitterWPF Client - Bugs? Contact Mark.Rowe@microsoft.com"});
			AllReadingsWithTopic.Add(new Payload()
			{
				CreatedAt = DateTime.UtcNow,
				Text = "Setting above are Pulled from .CONFIG or edit manually here"
			});
			LoadFromConfigIfAvailable();

		}

		private void LoadFromConfigIfAvailable()
		{
			OAuthToken = ConfigurationManager.AppSettings["oauth_token"];
			OAuthTokenSecret= ConfigurationManager.AppSettings["oauth_token_secret"];
			OAuthCustomerKey = ConfigurationManager.AppSettings["oauth_consumer_key"];
			OAuthConsumerSecret= ConfigurationManager.AppSettings["oauth_consumer_secret"];
			SearchGroups = ConfigurationManager.AppSettings["twitter_keywords"];
			ClearUnfoundSentiment = !string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["clear_all_with_undefined_sentiment"]) ?
				Convert.ToBoolean(ConfigurationManager.AppSettings["clear_all_with_undefined_sentiment"])
				: false;
			SendExtendedInformation = !string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["send_extended_information"]) ?
			Convert.ToBoolean(ConfigurationManager.AppSettings["send_extended_information"])
			: false;

			RequireAll = ConfigurationManager.AppSettings["match_mode"] == "all";

			EventHubConnectionString = ConfigurationManager.AppSettings["EventHubConnectionString"];
			EventHubName = ConfigurationManager.AppSettings["EventHubName"];
		}

		public RelayCommand StartStop
		{
			get
			{
				return new RelayCommand(() => {

					if (EventHubConnectionString.ContainsIgnoreCase("entitypath"))
					{
						MessageBox.Show("Please remove 'entitypath=' and the value from your connection string");
						return;
					}

					var isRunning = CurrentColor == RunColor;
					CurrentColor = isRunning ? StopColor :RunColor;
					var shouldRun = !isRunning;
				if (shouldRun)
				{
						//
						Run();
				//});
					
						
					}
					else
					{
						Stop(); 
					}
				});
			}
		}
		private void RegisterAggregates()
		{
			GalaSoft.MvvmLight.Messaging.Messenger.Default.Register<Payload>(this, e => {

				App.Current.Dispatcher.Invoke((Action)delegate // <--- HERE
				{
					AllReadingsWithTopic.Insert(0, e);
				});
			});
		}
		private void Stop()
		{

			Tweet.keepRunning = false;
			Tweet = new Tweet();
		}
		IDisposable SendingPayload { get; set; }
		Tweet Tweet { get; set; }
		private async Task Run()
		{
			var keywords = SearchGroups.Contains("|") ? string.Join(",", SearchGroups.Split('|')) : SearchGroups;
			var config = new EventHubConfig();
			config.ConnectionString = EventHubConnectionString;
			config.EventHubName = EventHubName;

			Tweet = new Tweet();
			Tweet.keepRunning = true;
			var myEventHubObserver = new EventHubObserverWPF(config);
			
			var sendingPayload = Tweet.StreamStatuses(new TwitterConfig(OAuthToken, OAuthTokenSecret, OAuthCustomerKey, OAuthConsumerSecret,
				keywords, SearchGroups)).Select(tweet => Sentiment.ComputeScore(tweet, SearchGroups, RequireAll ? "all" : "any")).Select(tweet => new Payload { CreatedAt = tweet.CreatedAt, Topic = tweet.Topic, SentimentScore = tweet.SentimentScore, Author = tweet.UserName, Text = tweet.Text, SendExtended = SendExtendedInformation });
			if (ClearUnfoundSentiment)
			{
				sendingPayload = sendingPayload.Where(e => e.SentimentScore > -1);
			}
			sendingPayload = sendingPayload.Where(e => e.Topic != "No Match");
			SendingPayload = await Task<IDisposable>.Run(() =>
				{
					return SendingPayload = sendingPayload.ToObservable().Subscribe(myEventHubObserver);
				});
		}
	}
}