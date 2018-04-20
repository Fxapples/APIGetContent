using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace APIGetContent
{
    public partial class MainForm : Form
    {
        //初始化请求状态
        private RequestStatus _requestStatus = RequestStatus.初始化;
        private Thread _thread;
        public MainForm()
        {
            InitializeComponent();
            btnStart.Text = @"开  始";
            lblMessage.Text = @"请先填写URL地址再使用";
            txtContent.Text = string.Empty;
            chkRepeat.Checked = false;
            txtTime.Enabled = false;
            //txtURL.Text = @"https://app.fishlee.net/cn12306/doc/logintip";
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtURL.Text.Trim()))
            {
                MessageBox.Show(@"请先填写URL地址");
                return;
            }
            if (string.IsNullOrWhiteSpace(txtFileName.Text.Trim()))
            {
                MessageBox.Show(@"请先填写文件名");
                return;
            }
            if (chkRepeat.Checked)
            {
                decimal time;
                if (! decimal.TryParse(txtTime.Text, out time))
                {
                    txtTime.Focus();
                    MessageBox.Show(@"请输入正确的数字");
                    return;
                }
                if (time <= 0)
                {
                    txtTime.Focus();
                    MessageBox.Show(@"间隔时间必须大于0");
                    return;
                }
            }

            switch (_requestStatus)
            {
                case RequestStatus.初始化:
                case RequestStatus.停止请求:
                    
                    if (!string.IsNullOrWhiteSpace(txtContent.Text.Trim()))
                    {
                        var tip = MessageBox.Show(@"是否清空界面现有记录？", @"提示信息", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (tip == DialogResult.Yes)
                        {
                            txtContent.Text = string.Empty;
                        }
                    }
                    _thread = new Thread(SetContent) { IsBackground = true };
                    btnStart.Text = @"停  止";
                    _requestStatus = RequestStatus.开始请求;
                    SetMessageText("正在请求...");
                    _thread?.Start();
                    break;
                case RequestStatus.开始请求:
                    lblMessage.Text = @"停止请求";
                    //txtContent.Text = string.Empty;
                    btnStart.Text = @"开  始";
                    _requestStatus = RequestStatus.停止请求;
                    //_thread?.Abort();
                    //Application.ExitThread();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        /// <summary>
        /// 处理数据
        /// </summary>
        protected void SetContent()
        {
            try
            {
                SetMessageText("正在请求...");
                if (chkRepeat.CheckState != CheckState.Checked)
                {
                    var model = CreateGetRequest(txtURL.Text);
                    if (model.Type && !string.IsNullOrWhiteSpace(model.Content))
                    {
                        SetLogTextDelegate("成功：" + model.Content);
                        AddRequestResult(model.Content);
                    }
                    else
                    {
                        SetLogTextDelegate("失败：" + model.Content);
                    }
                    //SetLogTextDelegate("成功："+model.Content);
                    _requestStatus = RequestStatus.停止请求;
                    BeginInvoke(new MethodInvoker(delegate
                    {
                        btnStart.Text = @"开  始";
                    }));
                }
                else
                {
                    var num = 0;
                    while (_requestStatus == RequestStatus.开始请求 && chkRepeat.CheckState == CheckState.Checked)
                    {
                        num++;
                        var model = CreateGetRequest(txtURL.Text);
                        if (!model.Type)
                        {
                            //SetMessageText($"第{num}次：请求失败！");
                            SetLogTextDelegate($"失败{num}：{model.Content}");
                            _requestStatus = RequestStatus.停止请求;
                            BeginInvoke(new MethodInvoker(delegate
                            {
                                btnStart.Text = @"开  始";
                            }));
                            break;
                        }
                        if (model.Type && string.IsNullOrWhiteSpace(model.Content))
                        {
                            //SetMessageText($"第{num}次：内容重复，已跳过！");
                            SetLogTextDelegate($"无内容{num}：{model.Content}");
                            continue;
                        }
                        //SetMessageText($"第{num}次：请求成功！");
                        var result = AddRequestResult(model.Content);
                        if (result == 0)
                        {
                            SetLogTextDelegate($"跳过{num}：{model.Content}");
                        }
                        else if (result == 1)
                        {
                            SetLogTextDelegate($"成功{num}：{model.Content}");
                        }
                        else if (result == -1)
                        {
                            SetLogTextDelegate($"异常{num}：{model.Content}");
                        }

                        var time = decimal.Parse(txtTime.Text.Trim());
                        Thread.Sleep(Convert.ToInt32(time*1000));
                    }
                }
                SetMessageText("请求结束...");
            }
            catch (Exception ex)
            {
                MessageBox.Show(@"程序异常：" + ex);
            }
        }

        /// <summary>  
        /// Post提交获取返回数据  
        /// </summary>  
        /// <param name="url">网址</param>  
        /// <returns></returns>  
        protected string CreatePostRequest(string url)
        {
            try
            {
                HttpWebResponse response = null;
                var encoding = Encoding.GetEncoding("UTF-8");
                var data = encoding.GetBytes(url);

                // 准备请求,设置参数  
                var request = WebRequest.Create(url) as HttpWebRequest;
                if (request != null)
                {
                    request.Method = "POST";
                    request.ContentType = "text/plain";
                    request.ContentLength = data.Length;

                    var outstream = request.GetRequestStream();
                    outstream.Write(data, 0, data.Length);
                    outstream.Flush();
                    outstream.Close();
                    //发送请求并获取相应回应数据  

                    response = request.GetResponse() as HttpWebResponse;
                }
                //直到request.GetResponse()程序才开始向目标网页发送Post请求  
                var instream = response?.GetResponseStream();
                if (instream == null) return null;
                var sr = new StreamReader(instream, encoding);
                //返回结果网页(html)代码  
                var content = sr.ReadToEnd();
                return content;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"请求失败{ex}");
                return null;
            }

        }

        /// <summary>
        /// get方法
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public RequestModel CreateGetRequest(string url)
        {

            //string address = url + "?" + param;
            //Uri uri = new Uri(address);
            var webReq = WebRequest.Create(url);
            try
            {
                using (var webResp = (HttpWebResponse)webReq.GetResponse())
                {
                    using (var respStream = webResp.GetResponseStream())
                    {
                        if (respStream == null) return null;
                        using (var objReader = new StreamReader(respStream, Encoding.GetEncoding("UTF-8")))
                        {
                            var strRes = objReader.ReadToEnd();
                            return new RequestModel
                            {
                                Type = true,
                                Content = strRes
                            };
                        }
                    }
                }
            }
            catch (WebException ex)
            {
                return new RequestModel
                {
                    Type = false,
                    Content = "请求失败/r/n错误信息：" + ex.Message
                };
            }
        }

        //private bool GetDoc(string strTxtName)
        //{
        //    var url = System.IO.Directory.GetCurrentDirectory();
        //    string strError = "NativeError ";
        //    int nLength = strError.Length;
        //    StreamReader sr = new StreamReader(url + strTxtName);
        //    string strLine = sr.ReadToEnd();
        //}

        /// <summary>
        /// 添加到文件
        /// </summary>
        /// <param name="result"></param>
        private int AddRequestResult(string result)
        {
            try
            {
                var url = Directory.GetCurrentDirectory() + $"\\{txtFileName.Text.Trim()}.txt";
                if (!File.Exists(url))
                {
                    using (var fs = File.Create(url))
                    {
                        fs.Close();
                    }
                }
                var list = File.ReadAllLines(url, Encoding.UTF8);
                if (list.Contains(result))
                {
                    return 0;
                }
                var content = new List<string> { result };
                File.AppendAllLines(url, content, Encoding.UTF8);
                return 1;
            }
            catch (Exception)
            {
                return -1;
                //throw new Exception();
            }
            //using (var file = new FileStream(url, FileMode.Open))
            //{
            //  //file.
            //}
        }

        private delegate void MessageDelegate(string str);
        private void SetMessageText(string str)
        {
            if (lblMessage.InvokeRequired)
            {
                Invoke(new MessageDelegate(SetMessageText), str);
            }
            else
            {
                lblMessage.Text = str;
            }
        }

        private delegate void LogTextDelegate(string str);
        private void SetLogTextDelegate(string str)
        {
            if (txtContent.InvokeRequired)
            {
                Invoke(new LogTextDelegate(SetLogTextDelegate), str);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(txtContent.Text))
                {
                    txtContent.AppendText(str);
                }
                else
                {
                    txtContent.AppendText(Environment.NewLine + str);
                }
            }
        }
        private void chkRepeat_Click(object sender, EventArgs e)
        {
            if (chkRepeat.Checked)
            {
                txtTime.Text = @"1";
                txtTime.Enabled = true;
            }
            else
            {
                txtTime.Enabled = false;
            }
        }
    }

    /// <summary>
    /// HTTP请求状态
    /// </summary>
    public enum RequestStatus
    {
        初始化 = 0, //初始化
        开始请求 = 1, //开始请求
        停止请求 = 2 //停止请求
    }
    /// <summary>
    /// 返回结果
    /// </summary>
    public class RequestModel
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Type { get; set; }
        /// <summary>
        /// 返回内容
        /// </summary>
        public string Content { get; set; }
    }
}
