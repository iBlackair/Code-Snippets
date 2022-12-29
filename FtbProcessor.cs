using Anticaptcha_example.Api;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using HtmlAgilityPack;

namespace Minecraft_Voter
{
    public class FtbProcessor
    {


        private static string WebsiteToken { get; set; }
        private static string CaptchaToken { get; set; }

        public static string ClientKey { get; set; }
        public static string FilePath { get; set; }
        public static string BaseUrl { get; set; }
		
        public FtbProcessor(string clientKey, string filepath, string baseurl)
        {
            ClientKey = clientKey;
            BaseUrl = baseurl;
            FilePath = filepath;
        }   
        private async Task<string> InitialRequest()
        {
            var Head = ApiHelper.ApiClient.DefaultRequestHeaders;
            Head.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9");
            Head.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate, br");
            Head.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
            Head.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36");


            string Url = "/server/z7RYUyxE/vote";
            using (HttpResponseMessage response = await ApiHelper.ApiClient.GetAsync(BaseUrl + Url))
            {
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    string Token = HtmlFetchOne(result, "/html/body/main/article/section[2]/div/form/input").Attributes["value"].Value;
                    if (Token != null)
                        WebsiteToken = Token;

                    return result;

                }
                else
                    throw new Exception(response.ReasonPhrase);
            }

        }


        public  HtmlNode HtmlFetchOne(string WebDoc, string XPath)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(WebDoc);

            HtmlNode ReturnValue = htmlDoc.DocumentNode.SelectSingleNode(XPath);

            if (ReturnValue == null)
                return null;
            else
                return ReturnValue;
        }


        private  async Task<JToken> CaptchaChallengeCreate()
        {
            ApiHelper.ApiClient.DefaultRequestHeaders.Clear();

            var Headers = ApiHelper.ApiClient.DefaultRequestHeaders;
            #region Headers
            Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/javascript"));
            Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
            Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
            Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));
            Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US"));
            Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en"));

            Headers.TryAddWithoutValidation("User-Agent","Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36");
            Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
            Headers.TryAddWithoutValidation("D", "1");
            Headers.Referrer = new Uri("https://ftbservers.com/server/z7RYUyxE/vote");
            Headers.TryAddWithoutValidation("Connection","keep-alive");
            Headers.TryAddWithoutValidation("Host","ftbservers.com");
            #endregion
            string Url = "/captcha/challenge/create";

            using (HttpResponseMessage response = await ApiHelper.ApiClient.GetAsync(BaseUrl + Url))
            {
                if (response.IsSuccessStatusCode)
                {
                    JToken token = JToken.Parse(await response.Content.ReadAsStringAsync());
                    if (token != null)
                        CaptchaToken = token["token"].ToString();
                    return token["image"];
                }
                else
                    throw new Exception(response.ReasonPhrase);
            }

        }

        private  async Task<string> SolveImageCaptcha(Uri ImageUrl)
        {
            if (DownloadRemoteImageFile(ImageUrl, FilePath))
            {
                Trace.WriteLine("FTB Image Aqcuired, Starting Image To Text");
                var api = new ImageToText
                {
                    ClientKey = ClientKey,
                    FilePath = FilePath,
                    Math = 0,
                    Case = true,
                    Phrase = true
                };
                if (!api.CreateTask())
                    return "Failed Task Create";
                else if (!api.WaitForResult())
                    return "Failed To Solve";
                else
                {
                    return api.GetTaskSolution().Text; ;
                }
            }
            else
                throw new Exception("FUBAR");
        }

        private  bool DownloadRemoteImageFile(Uri uri, string fileName)
        {
            if(File.Exists(fileName))
                File.Delete(fileName);
            var folderPath = fileName.Replace("FtbImage.jpg", "");
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
            else
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
                HttpWebResponse response;
                try
                {
                    response = (HttpWebResponse)request.GetResponse();
                }
                catch (Exception)
                {
                    return false;
                }

                if ((response.StatusCode == HttpStatusCode.OK ||
                    response.StatusCode == HttpStatusCode.Moved ||
                    response.StatusCode == HttpStatusCode.Redirect) &&
                    response.ContentType.StartsWith("image", StringComparison.OrdinalIgnoreCase))
                {

                    // if the remote file was found, download it
                    using (Stream inputStream = response.GetResponseStream())
                    using (Stream outputStream = File.OpenWrite(fileName))
                    {
                        byte[] buffer = new byte[4096];
                        int bytesRead;
                        do
                        {
                            bytesRead = inputStream.Read(buffer, 0, buffer.Length);
                            outputStream.Write(buffer, 0, bytesRead);
                        } while (bytesRead != 0);
                    }
                    return true;
                }
                else
                    return false;
            }
            return false;
        }


        public  async Task<bool> Vote()
        {
            _ = await InitialRequest();
            Uri ImageUrl = new Uri(BaseUrl + await CaptchaChallengeCreate());
            var CaptchaRes = await SolveImageCaptcha(ImageUrl);

            ApiHelper.ApiClient.DefaultRequestHeaders.Clear();

            var Headers = ApiHelper.ApiClient.DefaultRequestHeaders;
            #region Headers
            Headers.TryAddWithoutValidation("Host", "ftbservers.com");
            Headers.TryAddWithoutValidation("Connection", "keep-alive");
            Headers.TryAddWithoutValidation("Content-Length", "198");
            Headers.TryAddWithoutValidation("Cache-Control", "max-age=0");
            Headers.TryAddWithoutValidation("sec-ch-ua", "Not?A_Brand; v = 8, Chromium; v = 108 Google Chrome; v = 108");
            Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
            Headers.TryAddWithoutValidation("sec-ch-ua-platform", "Windows");
            Headers.TryAddWithoutValidation("Origin", "https://ftbservers.com");
            Headers.TryAddWithoutValidation("DNT", "1");
            Headers.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
            Headers.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");
            Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36");
            Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9");
            Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
            Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
            Headers.TryAddWithoutValidation("Sec-Fetch-User", "?1");
            Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
            Headers.TryAddWithoutValidation("Referer", "https://ftbservers.com/server/Z5hXc4vx/vote");
            Headers.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate, br");
            Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
            #endregion
            string Url = "/server/z7RYUyxE/vote";
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("web_server_vote[username]", "Random"),
                new KeyValuePair<string, string>("web_server_vote[bonus]", "Yes"),
                new KeyValuePair<string, string>("captcha-token", CaptchaToken),
                new KeyValuePair<string, string>("captcha-response", CaptchaRes),
                new KeyValuePair<string, string>("web_server_vote[_token]", WebsiteToken),
            });

            using (HttpResponseMessage response = await ApiHelper.ApiClient.PostAsync(BaseUrl + Url, formContent))
            {
                if (response.IsSuccessStatusCode)
                {
                    bool IsErrored = CheckFtbForErrors(await response.Content.ReadAsStringAsync()).Result.Item1;
                    if(IsErrored)
                    {
                        Trace.WriteLine("Failed");
                        return false;
                    }
                    else
                    {
                        Trace.WriteLine("Success");
                        return true;
                    }
                }
                else
                    throw new Exception(response.ReasonPhrase);
            }
        }

        private  async Task<(bool, string)> CheckFtbForErrors(string WebText)
        {
            int Timeout = 0;
            (bool, string) Result = (false, string.Empty);
            string[] Xpaths =
            {
                "/html/body/main/article/section[2]/div/form/div[1]/div[2]/ul/li",
                "/html/body/main/article/section[2]/div/form/div[3]/ul/li"
            };

            string[] Errors =
            {
                "You must enter a valid Minecraft username to vote",
                "You may only vote once per day",
                "Please complete the challenge to continue",
                "The response entered does not match the given code"
            };
            while (Timeout <= 10000)
            {
                var htmlDoc = new HtmlAgilityPack.HtmlDocument();
                htmlDoc.LoadHtml(WebText);

                for (int i = 0; i < Xpaths.Count(); i++)
                {
                    HtmlNode UserError = htmlDoc.DocumentNode.SelectSingleNode(Xpaths[i]);
                    for (int j = 0; j < Errors.Count(); j++)
                        if (UserError != null)
                            if (UserError.InnerHtml == Errors[j])
                            {
                                Result = (true, Errors[j]);
                                break;
                            }
                }
                await Task.Delay(500);
                Timeout += 1000;
            }
            return Result;
        }
    }
}
