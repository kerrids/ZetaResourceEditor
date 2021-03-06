﻿namespace ZetaResourceEditor.RuntimeBusinessLogic.Translation
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Globalization;
	using System.IO;
	using System.Net;
	using System.Text;
	using System.Web;
	using System.Xml;
	using Newtonsoft.Json;
	using Properties;
	using WebServices;
	using Zeta.EnterpriseLibrary.Common.Collections;

	public class GoogleRestfulTranslationEngine :
		ITranslationEngine
	{
		public bool Has2AppIDs
		{
			get { return false; }
		}

		public string AppID1Name
		{
			get { return Resources.GoogleRestfulTranslationEngine_AppID1Name_App_ID; }
		}

		public string AppID2Name
		{
			get { return Resources.GoogleRestfulTranslationEngine_AppID1Name_App_ID; }
		}

		public bool IsUserSelectable
		{
			get { return true; }
		}

		public bool IsDefault
		{
			get { return true; }
		}

		public string UniqueInternalName
		{
			get { return @"9efb237c-ce92-450f-9ef1-850091762d63"; }
		}

		public string UserReadableName
		{
			get { return Resources.GoogleRestfulTranslationEngine_UserReadableName_Google_Translate; }
		}

		public bool AreAppIDsSyntacticallyValid(string appID, string appID2)
		{
			return !string.IsNullOrEmpty(appID);
		}

		private static XmlDocument makeRestCallPost(
			string appID,
			string url,
			ICollection<Pair<string, string>> parameters)
		{
			if (!url.EndsWith(@"?")) url += @"?";

			parameters.Add(new Pair<string, string>(@"key", appID));
			//parameters.Add(new Pair<string, string>(@"userip", currentIPAddress));

			var ps = string.Empty;

			// ReSharper disable LoopCanBeConvertedToQuery
			foreach (var pair in parameters)
			// ReSharper restore LoopCanBeConvertedToQuery
			{
				if (!string.IsNullOrEmpty(pair.Value))
				{
					var p =
						string.Format(
							@"{0}={1}&",
							pair.Name,
							HttpUtility.UrlEncode(pair.Value));

					ps += p;
				}
			}

			ps = ps.TrimEnd('?', '&');

			// --

			var request = (HttpWebRequest)WebRequest.Create(url);
			request.Referer = string.Format(@"http://www.zeta-resource-editor.com/rest?{0:N}", Guid.NewGuid());

			WebServiceManager.Current.ApplyProxy(request);

			// Encode all the source data.
			var postSourceData = ps;
			request.Method = @"POST";
			request.ContentType = @"application/x-www-form-urlencoded";
			request.ContentLength = postSourceData.Length;
			request.UserAgent = @"Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1)";

			// http://code.google.com/intl/de/apis/language/translate/v2/using_rest.html
			request.Headers.Add(@"X-HTTP-Method-Override", @"GET");

			var writeStream = request.GetRequestStream();
			var bytes = Encoding.UTF8.GetBytes(postSourceData);
			writeStream.Write(bytes, 0, bytes.Length);
			writeStream.Close();

			// --

			string rawResult;

			// If the following fails, you must add this to web.config:
			// <configuration> 
			//		<system.net> 
			//			<settings> 
			//				<httpWebRequest useUnsafeHeaderParsing="true" /> 
			//			</settings> 
			//		</system.net> 
			// </configuration>
			using (var response = (HttpWebResponse)request.GetResponse())
			using (var responseStream = response.GetResponseStream())
			{
				if (responseStream == null)
				{
					return new XmlDocument();
				}
				else
				{
					using (var readStream = new StreamReader(responseStream, Encoding.UTF8))
					{
						rawResult = readStream.ReadToEnd();
					}
				}
			}

			return JsonConvert.DeserializeXmlNode(rawResult, @"root");
		}

		private TranslationLanguageInfo[] _sourceLanguages;

		public TranslationLanguageInfo[] GetSourceLanguages(string appID, string appID2)
		{
			if (_sourceLanguages == null)
			{
				var result = new List<TranslationLanguageInfo>();

				var json = makeRestCallPost(
					appID,
					@"https://www.googleapis.com/language/translate/v2/languages",
					new List<Pair<string, string>>());

				// --

				var nodes = json.SelectNodes(@"/root/data/languages/language");

				if (nodes != null)
				{
					// ReSharper disable LoopCanBeConvertedToQuery
					foreach (XmlNode xmlNode in nodes)
					// ReSharper restore LoopCanBeConvertedToQuery
					{
						var languageCode = xmlNode.InnerText;
						var ci = BingSoapTranslationEngine.IntelligentGetCultureInfo(languageCode);

						if (ci != null)
						{
							result.Add(
								new TranslationLanguageInfo
								{
									LanguageCode = languageCode,
									UserReadableName = ci.DisplayName,
								});
						}
					}
				}

				_sourceLanguages = result.ToArray();
			}

			return _sourceLanguages;
		}

		public TranslationLanguageInfo[] GetDestinationLanguages(string appID, string appID2)
		{
			return GetSourceLanguages(appID, appID2);
		}

		public int MaxArraySize
		{
			get { return 25; }
		}

		public string AppIDLink
		{
			get { return @"http://zeta.li/zre-translation-appid-google"; }
		}

		public string[] TranslateArray(
			string appID,
			string appID2,
			string[] texts,
			string sourceLanguageCode,
			string destinationLanguageCode,
			string[] wordsToProtect,
			string[] wordsToRemove)
		{
			var dic =
				new List<Pair<string, string>>
					{
						new Pair<string, string>(@"source", sourceLanguageCode),
						new Pair<string, string>(@"target", destinationLanguageCode),
					};

			// ReSharper disable LoopCanBeConvertedToQuery
			foreach (var text in texts)
			// ReSharper restore LoopCanBeConvertedToQuery
			{
				dic.Add(
					new Pair<string, string>(
						@"q",
						TranslationHelper.ProtectWords(
							TranslationHelper.RemoveWords(text, wordsToRemove), wordsToProtect)));
			}

			var json =
				makeRestCallPost(
					appID,
					@"https://www.googleapis.com/language/translate/v2",
					dic);

			// --

			var result = new List<string>();

			var nodes = json.SelectNodes(@"/root/data/translations/translatedText");

			if (nodes != null)
			{
// ReSharper disable LoopCanBeConvertedToQuery
				foreach (XmlNode xmlNode in nodes)
// ReSharper restore LoopCanBeConvertedToQuery
				{
					var translatedText = xmlNode.InnerText;

					result.Add(
						TranslationHelper.UnprotectWords(
							translatedText,
							wordsToProtect));
				}
			}

			return result.ToArray();
		}

		public bool isSourceLanguageSupported(string appID, string appID2, string languageCode)
		{
			return TranslationHelper.IsSupportedLanguage(languageCode, GetSourceLanguages(appID, appID2));
		}

		public bool isDestinationLanguageSupported(string appID, string appID2, string languageCode)
		{
			return TranslationHelper.IsSupportedLanguage(languageCode, GetDestinationLanguages(appID, appID2));
		}

		public string MapCultureToSourceLanguageCode(string appID, string appID2, CultureInfo cultureInfo)
		{
			return BingSoapTranslationEngine.DoMapCultureToLanguageCode(GetSourceLanguages(appID, appID2), cultureInfo);
		}

		public string MapCultureToDestinationLanguageCode(string appID, string appID2, CultureInfo cultureInfo)
		{
			return BingSoapTranslationEngine.DoMapCultureToLanguageCode(GetDestinationLanguages(appID, appID2), cultureInfo);
		}

		public string Translate(
			string appID, 
			string appID2, 
			string text, 
			string sourceLanguageCode, 
			string destinationLanguageCode, 
			string[] wordsToProtect, 
			string[] wordsToRemove)
		{
			var json =
				makeRestCallPost(
					appID,
					@"https://www.googleapis.com/language/translate/v2",
					new List<Pair<string, string>>
						{
							new Pair<string, string>(@"source", sourceLanguageCode),
							new Pair<string, string>(@"target", destinationLanguageCode),
							new Pair<string, string>(
								@"q",
								TranslationHelper.ProtectWords(
									TranslationHelper.RemoveWords(text, wordsToRemove), wordsToProtect))
						});

			//var responseStatusNode = json.SelectSingleNode(@"/root/data/responseStatus");
			//var statusCode = ConvertHelper.ToInt32(
			//    responseStatusNode == null
			//        ? string.Empty
			//        : responseStatusNode.InnerText);

			var translatedTextNode = json.SelectSingleNode(@"/root/data/translations/translatedText");
			//var responseDetailsNode = json.SelectSingleNode(@"/root/data/responseDetails");

			return
				TranslationHelper.UnprotectWords(
				//statusCode == 200
						 (translatedTextNode == null ? string.Empty : translatedTextNode.InnerText),
				//: (responseDetailsNode == null ? string.Empty : responseDetailsNode.InnerText),
					wordsToProtect);
		}

		public bool SupportsArrayTranslation
		{
			get { return true; }
		}
	}
}