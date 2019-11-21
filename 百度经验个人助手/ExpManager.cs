﻿
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Windows.Web.Http;
using Windows.Networking;
using Windows.Foundation;
using System.Text;
using System.Text.RegularExpressions;
using Windows.Storage.Streams;
using System.Threading.Tasks;
using System.IO;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using System.Collections;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.Web.Http.Headers;
using HttpClient = Windows.Web.Http.HttpClient;
using HttpMethod = Windows.Web.Http.HttpMethod;
using HttpRequestMessage = Windows.Web.Http.HttpRequestMessage;
using HttpResponseMessage = Windows.Web.Http.HttpResponseMessage;
using System.Threading;

namespace 百度经验个人助手
{
    //ContentEntry is moved to StorageManager

    public class RewardExpEntry
    {
        public RewardExpEntry(string name, decimal money, string pageUrl, string queryId)
        {
            Name = name;
            Money = money;
            PageUrl = pageUrl;
            QueryId = queryId;
        }
        public string Name { get; set; }
        public decimal Money { get; set; }
        public string PageUrl { get; set; }
        public string QueryId { get; set; }
        public string Info
        {
            get
            {
                return "ID: " + QueryId + " 链接: " + PageUrl;
            }
        }
    }


    public static class ExpManager
    {
        public static bool isCookieValid = false;
        public static bool isVerifying = false;

        public static string cookie;
        public static HttpClient client;
        public static string htmlMain;

        //always show the newest Main Inf
        public static string NewMainUserNameDecoded
        {
            get
            {
                if (newMainUserName != null) return Uri.UnescapeDataString(newMainUserName);
                else return ""; //to check
            }
        }
        public static string newMainUserName;
        public static string newMainIndexHuiXiang;
        public static string newMainIndexYiuZhi;
        public static string newMainIndexYuanChuang;
        public static string newMainIndexHuoYue;
        public static string newMainIndexHuDong;
        public static string newMainExpCount;
        public static string newMainPortraitUrl;
        public static string newMainBdStoken; // not show to users. for reward request.
        public static string newMainBdstt; // not show to users. for reward request.
        public static BitmapImage newMainPortrait;

        public static DataPack currentDataPack; // mains are set but not used
        public static string[] htmlContentPages;
        public static ObservableCollection<ContentExpEntry> contentExpsSearched;


        public static ObservableCollection<RewardExpEntry> rewardExps;
        public static HashSet<string> rewardExpIDs;
        public static ObservableCollection<RewardExpEntry> rewardExpsSearched;



        #region 常量字符串
        public static string regexMainUserName = "//www.baidu.com/p/(.*?)\\?from=jingyan";
        public static string regexMainIndexHuiXiang = "<span class=\"huixiang-value\" style=\".*?\">(.*?)</span>";
        public static string regexMainIndexYiuZhi = "<span class=\"quality-value value\" style=\".*?\">(.*?)</span>";
        public static string regexMainIndexYuanChuang = "<span class=\"origin-value value\" style=\".*?\">(.*?)</span>";
        public static string regexMainIndexHuoYue = "<span class=\"active-value value\" style=\".*?\">(.*?)</span>";
        public static string regexMainIndexHuDong = "<span class=\"interact-value value\" style=\".*?\">(.*?)</span>";
        public static string regexMainExpCount = "<span class=\"exp-num\">(.*?)</span>";
        public static string regexMainPortraitUrl = "src=\"(http[s]{0,1}://himg.bdimg.com/sys/portrait/item/.*?)\"";
        public static string regexMainBdStoken = "\"BdStoken\"[ \\s]*:[\\s]*\"([\\d\\w]+)\"";
        public static string regexMainBdstt = "\"bdstt\"[ \\s]*:[\\s]*\"([\\d\\w]+)\"";
        public static string regexContentExpTitleAndUrl = "<a class=\"f14\" target=\"_blank\" title=\"(.*?)\" href=\"(.*?)\">";
        public static string regexContentExpView = "<span class=\"view-count\">(\\d*?)</span>";
        public static string regexContentExpVote = "<span class=\"vote-count\">(\\d*?)</span>";
        public static string regexContentExpCollect = "<span class=\"favc-count\">(\\d*?)</span>";
        public static string regexContentExpDate = "<span class=\"f-date\">(.*?)<\\/span>";
        public static string regexRewardExpAll = "<span class=\"cash\" style=\".*?\">[\\S]{1,4}?([\\d\\.]+)" +
                                                 "</span><a class=\"title query-item-id\"[^<]*? data-queryId=\"(.*?)\">(.*?)</a>";

        public static string urlPrefix = "https://jingyan.baidu.com";
        public static string[] requiredCookieNames =
        {
            "BDUSS"
           /* "BAIDUID", "BIDUPSID", *///"PSTM",//"PS_REFER", "bdshare_firstime",
            //"__cfduid", "MCITY",//"BDORZ", "H_PS_PSSID", "PSINO"
        };
        public static string setcookieFailedInfo =
                "添加失败。请检查Cookie是否包含：\n临时身份凭证 \"BDUSS\""
            ;
        #endregion

        /// <summary>
        /// 初始化HttpClient，新建rewardExps, rewardExpsSearched, contentExpsSearched
        /// </summary>
        public static void Init()
        {
            //URL is not default
            //referrer is not default //try ignore this ?
            //cookie is not default //set later

            //dont use client now.
            client = new HttpClient();
            client.DefaultRequestHeaders.Host = new HostName("jingyan.baidu.com");
            client.DefaultRequestHeaders.Accept.ParseAdd("*/*");
            client.DefaultRequestHeaders.Connection.ParseAdd("keep-alive");
            client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.8,zh-TW;q=0.7,zh-HK;q=0.5,en-US;q=0.3,en;q=0.2");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:59.0) Gecko/20100101 Firefox/59.0");

            contentExpsSearched = new ObservableCollection<ContentExpEntry>();
            rewardExps = new ObservableCollection<RewardExpEntry>();
            rewardExpIDs = new HashSet<string>();
            rewardExpsSearched = new ObservableCollection<RewardExpEntry>();


        }

        /// <summary>
        /// 输入Cookie字符串，提取需要的并设置HttpClient
        /// </summary>
        /// <param name="newCookie"></param>
        /// <returns></returns>
        public static bool SetCookie(string newCookie)
        {
            newCookie = newCookie.Trim();
            if (newCookie.Substring(newCookie.Length - 1) != ";") newCookie += ';';
            ExpManager.cookie = newCookie;

            MatchCollection mc = Regex.Matches(newCookie, "(\\w*?)=.*?;");
            foreach (string rcook in requiredCookieNames)
            {
                bool find = false;
                foreach (Match m in mc)
                {
                    if (m.Groups[1].ToString() == rcook)
                    {
                        find = true;
                        client.DefaultRequestHeaders.Cookie.TryParseAdd(m.Groups[0].ToString());
                        break;
                    }
                }
                if (!find)
                {
                    client.DefaultRequestHeaders.Cookie.Clear();
                    return false;
                }

            }
            return true;
        }



        #region ---获取Html---

        private static async Task GetMainSubStep_CookiedGetMain()
        {
            htmlMain = await ExpManager.SimpleRequestUrl(
                "https://jingyan.baidu.com/user/nuc/",
                "https://jingyan.baidu.com/"
            );

        }

        private static async Task<BitmapImage> GetMainSubStep_CookielessGetPic(string picUrl)
        {
            Uri myUri = new Uri(picUrl);
            HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, myUri);
            req.Headers.Host = new HostName(myUri.Host);
            req.Headers.Cookie.Clear();
            HttpResponseMessage response = null;
            try
            {
                response = await client.SendRequestAsync(req);
            }
            catch (Exception e)
            {
                //do nothing
            }

            if (response == null)
            {
                return null;
            }
            //HttpResponseMessage response
            if (response.StatusCode != HttpStatusCode.Ok)
            {
                return null;
            }
            IHttpContent icont = response.Content;

            IBuffer buffer = await icont.ReadAsBufferAsync();


            IRandomAccessStream irStream = new InMemoryRandomAccessStream();
            BitmapImage img = new BitmapImage();

            await irStream.WriteAsync(buffer);
            irStream.Seek(0);

            await img.SetSourceAsync(irStream);

            irStream.Dispose();
            icont.Dispose();
            response.Dispose();
            req.Dispose();

            return img;

        }
        private static async Task<bool> GetMainSubStep_ParseMain()
        {
            isCookieValid = false;
            Match matchUname = Regex.Match(htmlMain, regexMainUserName);
            if (matchUname.Groups[1].Value == "" || matchUname.Groups[1].Value == null)
            {
                bool isLengthSatisfied = false;
                foreach(var pair in client.DefaultRequestHeaders.Cookie)
                {
                    if (pair.Name.ToUpper() == "BDUSS" && pair.Value.Trim().Length == 192) isLengthSatisfied = true;
                }

                string guessReason = "";
                string shortReason = "";
                if (isLengthSatisfied)
                {
                    guessReason = "最可能的原因是从浏览器退出登录，或者切换账号了。";
                    shortReason = "可能是因为从获取Cookie的地方退出登录或切换账号";
                }
                else
                {
                    guessReason = "最可能的原因是没有复制完整BDUSS，完整的BDUSS有 192 个字符。";
                    shortReason = "完整的BDUSS有 192 个字符";
                }


                await Utility.ShowMessageDialog("Cookie不起作用" + shortReason,
                    matchUname.Groups[0].Value.ToString() + " " + matchUname.Groups[1].Value.ToString() + "\n"
                    + guessReason + "\n如果确认不是，那这是意外的问题。");

                bool isSelfProblem = await Utility.ShowConfirmDialog("确认操作无误，Cookie含有192个字符长的BDUSS，也没有退出登录/切换账号", "如果确认问题不是cookie无效，或者操作失误，那么可以提交错误报告给开发者。", "可能是没设置好", "我确认操作没问题");
                if (!isSelfProblem)
                {
                    //REPORT
                    string relvars = "regex=" + regexMainUserName + "\n" + "htmlMain=" + htmlMain;
                    await Utility.FireErrorReport("用户名获取失败", relvars);

                    Application.Current.Exit();
                }
                return false;
            }
            newMainUserName = matchUname.Groups[1].Value;

            Match matchHuix = Regex.Match(htmlMain, regexMainIndexHuiXiang);
            newMainIndexHuiXiang = matchHuix.Groups[1].Value;

            Match matchYuanc = Regex.Match(htmlMain, regexMainIndexYuanChuang);
            newMainIndexYuanChuang = matchYuanc.Groups[1].Value;

            Match matchYiuz = Regex.Match(htmlMain, regexMainIndexYiuZhi);
            newMainIndexYiuZhi = matchYiuz.Groups[1].Value;

            Match matchHud = Regex.Match(htmlMain, regexMainIndexHuDong);
            newMainIndexHuDong = matchHud.Groups[1].Value;

            Match matchHuoy = Regex.Match(htmlMain, regexMainIndexHuoYue);
            newMainIndexHuoYue = matchHuoy.Groups[1].Value;

            Match matchENum = Regex.Match(htmlMain, regexMainExpCount);
            newMainExpCount = matchENum.Groups[1].Value;

            Match matchPUrl = Regex.Match(htmlMain, regexMainPortraitUrl);
            newMainPortraitUrl = matchPUrl.Groups[1].Value;

            Match matchBdStoken = Regex.Match(htmlMain, regexMainBdStoken);
            newMainBdStoken = matchBdStoken.Groups[1].Value;

            Match matchBdstt = Regex.Match(htmlMain, regexMainBdstt);
            newMainBdstt = matchBdstt.Groups[1].Value;

            isCookieValid = true;
            return true;
        }

        public static async Task<bool> GetMain()
        {
            await GetMainSubStep_CookiedGetMain();
            if (!await GetMainSubStep_ParseMain()) return false; //parse error

            newMainPortrait = await GetMainSubStep_CookielessGetPic(newMainPortraitUrl); //possible to be NULL, but MainPage will handle this.
            return true;
        }

        #endregion

        #region ---更新信息---




        private static void GetContentsSubStep_NewDataPack()
        {

            // 设置新的数据包，自动将MainInf复制，设置时间，新建ObservableCollection。

            currentDataPack = new DataPack();
            currentDataPack.SafeSetUserName(newMainUserName);
            currentDataPack.mainExpCount = newMainExpCount;
            currentDataPack.mainIndexHuDong = newMainIndexHuDong;
            currentDataPack.mainIndexHuiXiang = newMainIndexHuiXiang;
            currentDataPack.mainIndexHuoYue = newMainIndexHuoYue;
            currentDataPack.mainIndexYiuZhi = newMainIndexYiuZhi;
            currentDataPack.mainIndexYuanChuang = newMainIndexYuanChuang;
            currentDataPack.date = DateTime.Now;

            currentDataPack.contentExps = new ObservableCollection<ContentExpEntry>();
            currentDataPack.contentExpsCount = Int32.Parse(newMainExpCount);
            currentDataPack.contentPagesCount = (currentDataPack.contentExpsCount + 19) / 20;
            htmlContentPages = new string[currentDataPack.contentPagesCount];

        }



        //private static string thread0Ret;
        //private static string thread1Ret;
        //private static string thread2Ret;
        //private static string thread3Ret;
        //private static string thread4Ret;
        private static async Task GetContentsSubStep_CookiedGetContentPage(int pg)
        {
            string result = await ExpManager.SimpleRequestUrl(
                "https://jingyan.baidu.com/user/nucpage/content?tab=exp&expType=published&pn=" + pg * 20,
                "https://jingyan.baidu.com/user/nuc"
            );

            lock (htmlContentPages)//not sure is necessary
            {
#if DEBUG
                Debug.WriteLine(Task.CurrentId + " : 成功进入htmlContentPages锁定区域");
#endif
                htmlContentPages[pg] = result;

            }
            //if (htmlContentPages[pg] == "FAILED") return false;
            //return true;
        }

        private static bool GetContentsSubStep_ParseContentPage(int pg) //TODO: why error showed page 0 Error, first page still get?
        {
            Utility.LogLocalEvent("ParseContentPage " + pg);
            string html = htmlContentPages[pg];
            Utility.varTrace["[exp]GetContentsSubStep_ParseContentPage_html"] = html;
            MatchCollection mcTitleAndUrl = Regex.Matches(html, regexContentExpTitleAndUrl);
            MatchCollection mcView = Regex.Matches(html, regexContentExpView);
            MatchCollection mcVote = Regex.Matches(html, regexContentExpVote);
            MatchCollection mcCollect = Regex.Matches(html, regexContentExpCollect);
            MatchCollection mcDate = Regex.Matches(html, regexContentExpDate);

            if (!(mcTitleAndUrl.Count == mcView.Count &&
                  mcView.Count == mcVote.Count &&
                  mcVote.Count == mcCollect.Count &&
                  mcCollect.Count == mcDate.Count))
            {
                return false;
            }
            if (mcTitleAndUrl.Count == 0)
            {
                return false;
            }
            for (int i = 0; i < mcTitleAndUrl.Count; ++i)
            {
                currentDataPack.contentExps.Add(new ContentExpEntry(
                    mcTitleAndUrl[i].Groups[1].Value,
                    urlPrefix + mcTitleAndUrl[i].Groups[2].Value,
                    int.Parse(mcView[i].Groups[1].Value),
                    int.Parse(mcVote[i].Groups[1].Value),
                    int.Parse(mcCollect[i].Groups[1].Value),
                    mcDate[i].Groups[1].Value
                ));
            }
            return true;
        }
        private static void GetContentsSubStep_CalcSum4()
        {
            currentDataPack.contentExpsViewSum = 0;
            currentDataPack.contentExpsVoteSum = 0;
            currentDataPack.contentExpsCollectSum = 0;
            for (int i = 0; i < currentDataPack.contentExps.Count; ++i)
            {
                currentDataPack.contentExpsViewSum += currentDataPack.contentExps[i].View;
                currentDataPack.contentExpsVoteSum += currentDataPack.contentExps[i].Vote;
                currentDataPack.contentExpsCollectSum += currentDataPack.contentExps[i].Collect;
            }

            currentDataPack.contentExpsView20 = 0;
            int max = 20;
            if (currentDataPack.contentExpsCount < 20) max = currentDataPack.contentExpsCount;
            for (int i = 0; i < max; ++i)
            {
                currentDataPack.contentExpsView20 += currentDataPack.contentExps[i].View;
            }
        }


        /// <summary>
        /// 更新信息
        /// </summary>
        /// <param name="textShow">要更新的文字</param>
        /// <returns></returns>
        public static async Task GetContents(TextBlock textShow, ListView itemShow)
        {
            GetContentsSubStep_NewDataPack();

            itemShow.ItemsSource = currentDataPack.contentExps;

            int pagesCount = ExpManager.currentDataPack.contentPagesCount;

            //并行任务数是5
            int onceTasksGoal = 5;
            for (int i = 0; i < pagesCount; i += onceTasksGoal)
            {
                textShow.Text = String.Format("更新中 ({0}/{1})", i, ExpManager.currentDataPack.contentPagesCount);

                int currentTasksCount = Math.Min(onceTasksGoal, pagesCount - i); //获取当前的任务数


                //await Utility.ShowMessageDialog("开始新建线程", "页号码 i=" + i + ",  建立" + currentTasksCount + "个");
                Task Task0 = GetContentsSubStep_CookiedGetContentPage(i);
                Task Task1 = null;
                Task Task2 = null;
                Task Task3 = null;
                Task Task4 = null;
                if (currentTasksCount > 1) Task1 = GetContentsSubStep_CookiedGetContentPage(i + 1);
                if (currentTasksCount > 2) Task2 = GetContentsSubStep_CookiedGetContentPage(i + 2);
                if (currentTasksCount > 3) Task3 = GetContentsSubStep_CookiedGetContentPage(i + 3);
                if (currentTasksCount > 4) Task4 = GetContentsSubStep_CookiedGetContentPage(i + 4);
                await Task0;
                if (Task1 != null) await Task1;
                if (Task2 != null) await Task2;
                if (Task3 != null) await Task3;
                if (Task4 != null) await Task4;
                //await Utility.ShowMessageDialog("线程等待完成","i="+i);

                //for (int j = i; j < i + currentTasksCount; ++j)
                //{
                //    if (htmlContentPages[j] == "FAILED") return false;  //如果获取出错，返回失败
                //}
                //如果解析出错，直接返回失败。
                for (int j = i; j < i + currentTasksCount; ++j)
                {
                    if (!GetContentsSubStep_ParseContentPage(j))
                    {
                        Utility.LogEvent("ERROR_ParseContentFailed");
                        await Utility.ShowMessageDialog("意外问题，程序收集错误后结束", "数据获取成功但是解析失败。获取页 " + i + " 无要寻找的经验条目，可能是用户中途退出登录，也可能是Baidu经验页面有调整（可能性最低）。"
                                                        + "\n一般情况下，这是一个容易解决的问题，看到此对话框可以截图给开发者并询问解决方法。");

                        Utility.LogEvent("ASSERT_UpdateExpNull");

                        //REPORT
                        string relvars = "page-id=" + i + "\nhtml content=" + htmlContentPages[j];
                        await Utility.FireErrorReport("更新EXP Content页面匹配失败", relvars);

                        App.Current.Exit();
                    }
                }
            }

            //检查和去除重复
            await currentDataPack.CheckRemoveDuplicate();

            //如果成功，计算calcsum4。
            GetContentsSubStep_CalcSum4();
        }
        #endregion

        #region 悬赏

        public static async Task<bool> CookielessGetReward(string tp, int cid, int pg)
        {
            string rewardUrl = "https://jingyan.baidu.com/patch?tab=" + tp + "&cid=" + cid + "&pn=" + pg * 15;

            string rewardPage = await ExpManager.SimpleRequestUrl(
                rewardUrl,
                "https://jingyan.baidu.com/"
            );

            return ParseReward(rewardPage, rewardUrl);
        }
        private static bool ParseReward(string html, string pageUrl)
        {
            MatchCollection mc = Regex.Matches(html, regexRewardExpAll);
            bool anyNew = false;
            lock (rewardExpIDs)
            {
                foreach (Match m in mc)
                {
                    string qid = m.Groups[2].Value;
                    if (rewardExpIDs.Contains(qid)) continue;
                    rewardExps.Add(new RewardExpEntry(
                        m.Groups[3].Value,
                        decimal.Parse(m.Groups[1].Value),
                        pageUrl, qid));
                    rewardExpIDs.Add(qid);
                    anyNew = true;
                }
                if (!anyNew) Utility.varTrace["[reward]rewardHtml"] = html;
            }
            if (!anyNew) return false;
            if (mc.Count > 0) return true;
            return false;
        }
        public static async Task<bool> CookiedGetReward(string queryId)
        {
            if (isCookieValid == false)
            {
                await Utility.ShowMessageDialog("领取功能需要设置Cookie", "领取操作不仅需要悬赏令ID, 还需要您的BDUSS. 请先设置Cookie.");
                return false;
            }
            HttpResponseMessage response = null;
            try
            {
                string url = "https://jingyan.baidu.com/patchapi/claimQuery?queryId="
                    + queryId + "&token=" + newMainBdStoken + "&timestamp=" + newMainBdstt;
                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, new Uri(url));
                req.Headers.Referer = new Uri("https://jingyan.baidu.com/patch");
                // ----- not working -----
                //KeyValuePair<string, string>[] urlParams = {
                //    new KeyValuePair<string, string>("queryId", queryId),
                //    new KeyValuePair<string, string>("token", newMainBdStoken),
                //    new KeyValuePair<string, string>("timestamp", newMainBdstt)
                //};
                //req.Content = new FormUrlEncodedContent(urlParams) as IHttpContent;
                response = await client.SendRequestAsync(req);
                string respstr = response.Content.ToString().Replace(" ", "");
                bool isGetSucceed = false;
                bool isCritical = false;
                if (respstr.IndexOf("\"errno\":0") >= 0)
                {
                    Utility.LogEvent("YES_TakeRewardSucceed");
                    respstr = "成功领取。请在 个人中心->悬赏经验->已领取 查看。";
                    isGetSucceed = true;
                }
                else if (respstr.IndexOf("\"errno\":302") >= 0)
                {
                    Utility.LogEvent("OK_TakeRewardAlready");
                    respstr = "你可能已经领取过经验。领取不成功(302)。";
                    //isGetSucceed = true;
                }
                else if (respstr.IndexOf("\"errno\":2") >= 0)
                {
                    Utility.LogEvent("ERROR_TakeRewardInvalid");
                    respstr = "身份验证失败，如果确定Cookie设定有效，可告知开发者 wang1223989563。错误码:2";
                    isCritical = true;
                }
                else
                {
                    Utility.LogEvent("ERROR_TakeRewardUnknown" + respstr);
                    respstr = "未知错误类型 (非302或2错误。可告知开发者 wang1223989563) \n错误信息: " + respstr;
                    isCritical = true;
                }

                bool enterEditor = false;
                if (isGetSucceed)
                {
                    enterEditor = await Utility.ShowConfirmDialog("领取成功", respstr + "\n是否进入编辑器?", "进入编辑器", "取消");
                }
                else{
                    await Utility.ShowMessageDialog("领取不成功", respstr);
                    if (isCritical)
                    {
                        //REPORT
                        string vars = "queryId=" + queryId + "\nrespstr=" + respstr + "\nheaders=" + req.Headers.ToString();
                        await Utility.FireErrorReport("领取失败", vars);
                    }
                }
                
                req.Dispose();
                return enterEditor;
            }
            catch (Exception e)
            {
                Utility.LogEvent("ERROR_TakeRewardException");
                await Utility.ShowDetailedError("领取未成功", e);
                
                //REPORT
                await Utility.FireErrorReport("领取程序崩溃", "queryId=" + queryId, e);

                return false;
            }
        }
        #endregion

        #region ---搜索数据---

        public static void SearchContentExps(string search, string order, int count)
        {
            string[] spieces = search.Trim().Split();
            for (int i = 0; i < spieces.Length; ++i)
            {
                spieces[i] = spieces[i].ToUpper();
            }
            Func<ContentExpEntry, bool> selector = (t) =>
            {
                foreach (string s in spieces)
                {
                    if (t.ExpName.ToUpper().Contains(s)) return true;
                }
                return false;
            };
            IEnumerable<ContentExpEntry> ic = currentDataPack.contentExps.Where(selector);

            if (order == "view")
                ic = ic.OrderBy(t => -t.View);
            else if (order == "viewinc")
                ic = ic.OrderBy(t => -t.ViewIncrease);
            else if (order == "collect")
                ic = ic.OrderBy(t => -t.Collect);
            else if (order == "new")
            {
                //Do Nothing
            }
            else if (order == "match")
            {
                Func<ContentExpEntry, int> sorter = (t) =>
                {
                    int matchCount = 0;
                    foreach (string s in spieces)
                    {
                        if (t.ExpName.ToUpper().Contains(s)) matchCount += 1;
                    }
                    return (-matchCount);
                };
                ic = ic.OrderBy(sorter);
            }

            contentExpsSearched.Clear();

            int curCount = 0;
            foreach (ContentExpEntry ce in ic)
            {
                contentExpsSearched.Add(ce);
                curCount++;
                if (curCount >= count) break;
            }
        }

        public static void SearchRewardExps(string search, string order, int count)
        {
            string[] spieces = search.Trim().Split();
            for (int i = 0; i < spieces.Length; ++i)
            {
                spieces[i] = spieces[i].ToUpper();
            }
            Func<RewardExpEntry, bool> selector = (t) =>
            {
                foreach (string s in spieces)
                {
                    if (t.Name.ToUpper().Contains(s)) return true;
                }
                return false;
            };
            IEnumerable<RewardExpEntry> ic = rewardExps.Where(selector);

            if (order == "money")
                ic = ic.OrderBy(t => -t.Money);
            else if (order == "short")
                ic = ic.OrderBy(t => t.Name.Length);
            else if (order == "new")
            {
                //Do Nothing
            }
            else if (order == "match")
            {
                Func<RewardExpEntry, int> sorter = (t) =>
                {
                    int matchCount = 0;
                    foreach (string s in spieces)
                    {
                        if (t.Name.ToUpper().Contains(s)) matchCount += 1;
                    }
                    return (-matchCount);
                };
                ic = ic.OrderBy(sorter);
            }

            rewardExpsSearched.Clear();

            int curCount = 0;
            foreach (RewardExpEntry ce in ic)
            {
                rewardExpsSearched.Add(ce);
                curCount++;
                if (curCount >= count) break;
            }
        }

        #endregion

        private static void setVerifying(bool val)
        {
            if (val == true)
            {
                isVerifying = true;
                App.currentMainPage.ShowLoading("Verifying...");
#if DEBUG
                Debug.WriteLine("# first task set isVerifying = true");
#endif
            }
            else
            {
                isVerifying = false;
                App.currentMainPage.HideLoading();
#if DEBUG
                Debug.WriteLine("# first task set isVerifying = false");
#endif
            }
        }

        //prepared private
        public static async Task<string> SimpleRequestUrl(string url, string referrer, string method = "GET", bool thisVerifying = false)
        {
            Utility.LogLocalEvent("SimpleRequestUrl " + url);
            HttpResponseMessage response = null;
            try
            {
                HttpRequestMessage req = null;
                if (method == "GET")
                {
                    req = new HttpRequestMessage(HttpMethod.Get, new Uri(url));
                }
                else
                {
                    req = new HttpRequestMessage(HttpMethod.Post, new Uri(url));
                }
                req.Headers.Referer = new Uri(referrer);
                response = await client.SendRequestAsync(req);
                req.Dispose();


                if (response.StatusCode != HttpStatusCode.Ok)
                {
                    Utility.LogEvent("ERROR_ResponseNot200-" + response.StatusCode);
                    await Utility.ShowMessageDialog("意外情况，返回状态不是200 OK", "返回状态: " + response.StatusCode.ToString() + "\n请向开发者（1223989563@qq.com）反映.");

                    // REPORT
                    string arelvars = "url=" + url + "\nreferrer=" + referrer + "\nisverifying=" + isVerifying;
                    await Utility.FireErrorReport("SimpleRequestUrl 返回非200 OK", arelvars);
                }

                IHttpContent icont = response.Content;
                IInputStream istream = await icont.ReadAsInputStreamAsync();

                StreamReader reader = new StreamReader(istream.AsStreamForRead(), Encoding.UTF8);
                string content = reader.ReadToEnd();
                reader.Dispose();
                istream.Dispose();
                icont.Dispose();
                response.Dispose();

                Utility.varTrace["[request][exp]SimpleRequestUrl_return"] = content;
                return content;
            }
            catch (Exception e)
            {
                if ((uint)e.HResult == 0x80072f76)
                {
                    if (e.Message.Contains("邮件标头") || e.Message.ToUpper().Contains("REQUESTED HEADER"))
                    {
                        if (!isVerifying || thisVerifying) // ------ first task will enter this and keep enter this until verified. ------
                        {

                            setVerifying(true); // set verifying state.
                            Utility.LogEvent("OK_VerifyingState");

                            string verifyResp = "";
                            while (!verifyResp.Replace(" ", "").Contains("{\"errno\":0"))
                            {
                                var authDialog = new ContentAuthenticationDialog();
                                authDialog.imageUrl = "https://jingyan.baidu.com/common/getVerifyCaptcha?t=" + DateTime.UtcNow.Ticks.ToString();
                                var result = await authDialog.ShowAsync();
                                if (result == ContentDialogResult.Primary || result == ContentDialogResult.None)
                                {
                                    setVerifying(false); // just believe users choice for now. if not working, another task might back to verifying state.
                                    return await SimpleRequestUrl(url, referrer, method);
                                }
                                else if (result == ContentDialogResult.Secondary)
                                {
                                    string vurl = "https://jingyan.baidu.com/submit/antispam?method=verify&vcode=" + authDialog.verifyKey;
                                    verifyResp = await SimpleRequestUrl(vurl, referrer, "POST", true);
                                }
                            }
                            Utility.LogEvent("YES_VerifySucceed");
                            App.currentMainPage.ShowNotify("验证通过", "所有中断的请求将会重新发送.");

                            setVerifying(false); // if succeed, cancel verifying state.
                            return await SimpleRequestUrl(url, referrer, method);
                        }
                        else   //  ---------------------- other task will wait until the verifying thread finish. ----------------------
                        {
#if DEBUG
                            Debug.WriteLine("# Thread start waiting. Failed url is: " + url);
#endif
                            while (isVerifying) await Task.Delay(500);
#if DEBUG
                            Debug.WriteLine("# Thread waiting done. retry url: " + url);
#endif
                            return await SimpleRequestUrl(url, referrer, method);
                        }
                    }
                    Utility.LogEvent("ERROR_Unresolved-0x80072f76");
                    await Utility.ShowMessageDialog("可能是被验证码阻挡，也可能是其它网络问题，程序结束。",
                        "错误代码：" + String.Format("{0:x8}", e.HResult) + "\n错误信息：\n\n" +
                        e.Message + "\n"
                        + "如果是验证码问题，请先打开浏览器，输入验证码，稍后再打开本程序。\n验证码问题在中文版系统中已修复，因此这条消息不该看到。开发者需要知道您的错误信息（1223989563@qq.com）");

                    // REPORT
                    string arelvars = "url=" + url + "\nreferrer=" + referrer + "\nisverifying=" + isVerifying;
                    await Utility.FireErrorReport("SimpleRequestUrl 0x80072f76非验证码", arelvars, e);

                    Application.Current.Exit();
                }
                else if ((uint)e.HResult == 0x80072efd)
                {
                    Utility.LogEvent("WARN-Exit-0x80072efd");
                    await Utility.ShowMessageDialog("80072efd，系统网络故障，程序结束。",
                        "\n错误信息：\n\n" + e.Message + "\n请百度80072efd寻找解决办法。该故障和系统网络设置有关。");
                    Application.Current.Exit();
                }
                else if ((uint)e.HResult == 0x80072eff)
                {
                    Utility.LogEvent("WARN-Exit-0x80072eff");
                    await Utility.ShowMessageDialog("80072eff，网络有连接，百度经验无响应",
                        "请确认能够访问 jingyan.baidu.com，关闭此对话框会重试失败的请求。\n错误信息：\n\n" + e.Message);
                    return await SimpleRequestUrl(url, referrer, method);
                }

                Utility.LogEvent("ERROR-UNEXPECTED:" + e.HResult.ToString());
                await Utility.ShowMessageDialog("网络故障，错误信息请留意", "关闭此提示框，程序收集错误信息。\n错误信息：\n\n" + e.Message);

                // REPORT
                string relvars = "url=" + url + "\nreferrer=" + referrer + "\nisverifying=" + isVerifying;
                await Utility.FireErrorReport("SimpleRequestUrl 网络故障", relvars, e);

                Application.Current.Exit();
                return ""; //should never reached.
            }
        }

        public static async Task<string> PostData(string url, string referrer, KeyValuePair<string, string>[] keyValues)
        {
            //HttpResponseMessage response = null;
            try
            {
                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, new Uri(url));

                HttpFormUrlEncodedContent cont = new HttpFormUrlEncodedContent(keyValues);

                req.Content = cont;

                if(referrer != "") req.Headers.Referer = new Uri(referrer);

                HttpResponseMessage hc = await client.SendRequestAsync(req);

                req.Dispose();

                return hc.Content.ToString();
            }
            catch (Exception ee)
            {
                return "ERROR: " + ee.HResult + " " + ee.Message;
            }
        }




    }
}
