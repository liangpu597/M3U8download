using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.IO;
using System.Xml;
using System.Text;
using Microsoft.WindowsAPICodePack.Taskbar;
using System.Threading;
//这里有两个命名空间！！！
namespace M3U8_Downloader  //这是默认的命名空间
{
    public partial class Form1 : Form
    {
        //任务栏进度条的实现。
        //库的名字叫做： Windows API Code Pack for Microsoft .NET Framework 需要进行额外下载 
        //这里已经进行了下载 在项目文件中 名字叫做：Microsoft.WindowsAPICodePack.dll 还有一个
        //还有一个 TaskbarProgressBarState 

        private TaskbarManager windowsTaskbar = TaskbarManager.Instance;
        
        //拖动窗口
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        public static extern bool SendMessage(IntPtr hwnd, int wMsg, int wParam, int lParam);
        public const int WM_SYSCOMMAND = 0x0112;
        public const int SC_MOVE = 0xF010;
        public const int HTCAPTION = 0x0002;

        [DllImport("kernel32.dll")]
        static extern bool GenerateConsoleCtrlEvent(int dwCtrlEvent, int dwProcessGroupId);
        [DllImport("kernel32.dll")]
        static extern bool SetConsoleCtrlHandler(IntPtr handlerRoutine, bool add);
        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int dwProcessId);
        [DllImport("kernel32.dll")]
        static extern bool FreeConsole();
        [DllImport("user32.dll")]
        public static extern bool FlashWindow(IntPtr hWnd,bool bInvert );


        int ffmpegid = -1;
        Double big = 0;
        Double small = 0;

        //不影响点击任务栏图标最大最小化
        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_MINIMIZEBOX = 0x00020000;  // Winuser.h中定义
                CreateParams cp = base.CreateParams;
                cp.Style = cp.Style | WS_MINIMIZEBOX;   // 允许最小化操作
                return cp;
            }
        }

        //public Form1()
        //{
        //    InitializeComponent();  //系统添加
        //    Init();  //作者添加的：初始化的时候就应该将相应的函数注册到委托事件中
        //    Control.CheckForIllegalCrossThreadCalls = false;  //禁止编译器对跨线程访问做检查
        //}

        private void textBox_Adress_DragEnter(object sender, DragEventArgs e)
        {

            e.Effect = DragDropEffects.All;
        }

        private void textBox_Adress_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.All;
        }
        private void textBox_Adress_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop, false) == true)
            {
                //获取拖拽的文件地址
                var filenames = (string[])e.Data.GetData(DataFormats.FileDrop);
                var hz = filenames[0].LastIndexOf('.') + 1;
                var houzhui = filenames[0].Substring(hz);//文件后缀名
                if (houzhui == "m3u8"||houzhui == "mkv"||houzhui == "avi"||houzhui == "mp4"||houzhui == "ts"||houzhui == "flv"||houzhui == "f4v"||
                    houzhui == "wmv"||houzhui == "wm"||houzhui == "mpeg"||houzhui == "mpg"||houzhui == "m4v"||houzhui == "3gp"||houzhui == "rm"||
                    houzhui == "rmvb" || houzhui == "mov" || houzhui == "qt" || houzhui == "m2ts" || houzhui == "m3u" || houzhui == "mts" || houzhui == "txt") //只允许拖入部分文件
                {
                    e.Effect = DragDropEffects.All;
                    string path = ((System.Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();
                    textBox_Adress.Text = path; //将获取到的完整路径赋值到textBox1
                }
                
            }        
            
        }

        private void button_Quit_Click(object sender, EventArgs e)
        {
            SaveSettings();
            try
            {
                if (Process.GetProcessById(ffmpegid) != null)
                {
                    if (MessageBox.Show("已启动下载进程，确认退出吗？\n（这有可能是强制的）", "请确认您的操作", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == System.Windows.Forms.DialogResult.Yes)
                    {
                        Stop();
                        MessageBox.Show("已经发送命令！\n若进程仍然存在则强制结束！", "请确认");
                        try
                        {
                            if (Process.GetProcessById(ffmpegid) != null)  //如果进程还存在就强制结束它
                            {
                                Process.GetProcessById(ffmpegid).Kill();
                                Dispose();
                                Application.Exit();
                            }
                        }
                        catch
                        {
                            Dispose();
                            Application.Exit();
                        }

                    }
                    else
                    {
                    }
                }
            }
            catch
            {
                Dispose();
                Application.Exit();
            }

        }

        private void button_ChangePath_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                textBox_DownloadPath.Text = folderBrowserDialog1.SelectedPath;
            }
        }

        private void button_OpenPath_Click(object sender, EventArgs e)
        {
            Process.Start(textBox_DownloadPath.Text);
        }

        private void linkLabel_Stop_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Stop();
        }

        //格式化大小输出
        public static String FormatFileSize(Double fileSize)
        {
            if (fileSize < 0)
            {
                throw new ArgumentOutOfRangeException("fileSize");
            }
            else if (fileSize >= 1024 * 1024 * 1024)
            {
                return string.Format("{0:########0.00} GB", ((Double)fileSize) / (1024 * 1024 * 1024));
            }
            else if (fileSize >= 1024 * 1024)
            {
                return string.Format("{0:####0.00} MB", ((Double)fileSize) / (1024 * 1024));
            }
            else if (fileSize >= 1024)
            {
                return string.Format("{0:####0.00} KB", ((Double)fileSize) / 1024);
            }
            else
            {
                return string.Format("{0} bytes", fileSize);
            }
        }

        //输出文本框改变后需要改变进度条的进度，也就是说 输出文本框一旦改变 需要改变这里的两个进度条的进度
        //这个文本框是定长的 而输出的是很长的 如果每次把输出都显示在屏幕上 可能会进行分页
        //所以 这里需要控制分页。
        //解决方法： 要么删除前面的  要么使用滚动
        private void textBox_Info_TextChanged(object sender, EventArgs e)
        {
            
            if (textBox_Info.GetLineFromCharIndex(textBox_Info.TextLength) + 1 > 14)    //如果当前总行数大于14 那么设置可以进行滚动
                textBox_Info.ScrollBars = ScrollBars.Vertical;   
            if (textBox_Info.GetLineFromCharIndex(textBox_Info.TextLength) + 1 <= 14)
                textBox_Info.ScrollBars = ScrollBars.None;                              //如果总行数小于的话 那么把滚动关闭

            //关于这个正则表达式：https://kb.cnblogs.com/page/91780/

            //总时长的显示等等，需要在ffmpeg中的输出查找才可以找得到
            //文本格式是: Duration: 00:45:22.56, start: 1.400000, bitrate: 0 kb/s  总时长是这个
            Regex duration = new Regex(@"Duration: (\d\d[.:]){3}\d\d", RegexOptions.Compiled | RegexOptions.Singleline);//取总视频时长
            label5.Text = "[总时长：" + duration.Match(textBox_Info.Text).Value.Replace("Duration: ", "") + "]";
            //在实际中 总时长只出现了一次 所以这里使用 match 而不使用 matches 这样的话 label5.text 的文本内容也就是只有一个


            //原格式：frame= 8776 fps=1194 q=-1.0 Lsize=   20341kB time=00:05:51.09 bitrate= 474.6kbits/s speed=47.8x 
            Regex regex = new Regex(@"(\d\d[.:]){3}\d\d", RegexOptions.Compiled | RegexOptions.Singleline);//取视频时长以及Time属性
            //var time = regex.Matches(textBox_forRegex.Text);
            var time = regex.Matches(textBox_Info.Text);
            //这两个文本框不一样

            Regex size = new Regex(@"[1-9][0-9]{0,}kB time", RegexOptions.Compiled | RegexOptions.Singleline);//取已下载大小
            //var sizekb = size.Matches(textBox_forRegex.Text);
            var sizekb = size.Matches(textBox_Info.Text);
           
            if (time.Count > 0 && sizekb.Count > 0)  //匹配到多次 取最下面的那一次 正则表达式还可以显示匹配的次数 使用 counts
            {
                //取匹配的最后一次 OfType<Match>().Last()
                //而大小是 string类型的：20341kB 这里把它转换成了MB 用来除以1024就可以了
                //MessageBox.Show(sizekb.OfType<Match>().Last().ToString().Replace("kB time", ""));
              //注意这里本来是KB 现在去掉末尾的KB 对于 FormatFileSize 函数 默认数值是B 他会根据数值大小进行转换， 所以 这里 KB -》B 需要先乘上1024  然后调用函数 函数根据结果自动给你计算出来
                label6.Text = "[已下载：" + time.OfType<Match>().Last() + "，" + FormatFileSize(Convert.ToDouble(sizekb.OfType<Match>().Last().ToString().Replace("kB time", "")) *1024) + "]";
            }
            //yuv420p, 1280x720 [SAR 1:1 DAR 16:9], 25 fps, 25 tbr, 90k tbn, 50 tbc
            
            //注意 虽然使用了Replace 但是原来的值根本没有改变 这里并不想改变原来的值 只是想要在另外一个label中显示而已
            //注意这里与上面的： match 只匹配一次 从头开始  Matches 则是匹配多次 

            Regex fps = new Regex(@", (\S+)\sfps", RegexOptions.Compiled | RegexOptions.Singleline);//取视频帧数
            Regex resolution = new Regex(@", \d{2,}x\d{2,}", RegexOptions.Compiled | RegexOptions.Singleline);//取视频分辨率

            //所以虽然这里分辨率有多个 但是只匹配一个 所以这里没有问题
            label7.Text = "[视频信息：" + resolution.Match(textBox_Info.Text).Value.Replace(", ","") + "，" + fps.Match(textBox_Info.Text).Value.Replace(", ", "") + "]";
            //这里有点怪啊 上面的已经对count>0的情况进行了改变了。
            if (time.Count > 0 && sizekb.Count > 0)  //防止程序太快 无法截取  这里的太快到底是怎样定义的呢？？？
            {
                //前面time 只是进行了匹配 而没有进行替换什么的
                try
                {
                    //label5是总时长的标签， 原来的是设置成这个样子： label5.Text = "[总时长：" + duration.Match(textBox_Info.Text).Value.Replace("Duration: ", "") + "]";
                    //Duration: 00:45:22.56, start: 1.400000, bitrate: 0 kb/s 
                    //先取前两位 这个是小时 把它转换成秒 * 3600 
                    //1秒=1000毫秒 所以最后会把小数点后面的除以1000 但是这里末尾是0.56 取最后两位数是56 转换成秒应该是56/100=0.56
                    Double All = Convert.ToDouble(Convert.ToDouble(label5.Text.Substring(5, 2)) * 60 * 60 + Convert.ToDouble(label5.Text.Substring(8, 2)) * 60
                + Convert.ToDouble(label5.Text.Substring(11, 2)) + Convert.ToDouble(label5.Text.Substring(14, 2)) / 100);
                    //这样加在一起的话 就有小数了 00:45:22.56 转换后变成了2722.56

                    //label6是已下载那个标签 同理 把已经下载的标签的转换成秒的形式
                    Double Downloaded = Convert.ToDouble(Convert.ToDouble(label6.Text.Substring(5, 2)) * 60 * 60 + Convert.ToDouble(label6.Text.Substring(8, 2)) * 60
                    + Convert.ToDouble(label6.Text.Substring(11, 2)) + Convert.ToDouble(label6.Text.Substring(14, 2)) / 100);

                    //if (All == 0) All = 1;  //防止被除数为零导致程序崩溃 这种情况基本上不存在 因为对于01 这种string Convert 后会变成1 而不是01
                    //Double Progress = (Downloaded / All) * 100; //进度条的显示是：已经下载的/总的  

                    //if (Progress > 100)  //防止进度条超过百分之百
                    //    Progress = 100;
                    //if (Progress < 0)  //防止进度条小于零……
                    //    Progress = 0;

                    //经过实践发现上述的情况基本不存在 所以直接进行
                    Double Progress = (Downloaded / All) * 100;   //这里两个都是double 类型的 相除的话不会进行舍去
                    //并且这里最后运行结束的时候 程序会自动停止 应该不存在 > 100 的情况
                    
                    ProgressBar.Value = Convert.ToInt32(Progress);
                    //windowsTaskbar.SetProgressValue(Convert.ToInt32(Progress), 100, this.Handle);        //这个是用于显示任务栏的进度的
                    //显示下载的进度值
                    label_Progress.Visible = true;
                    //{0：F2} 显示小数点后两位
                    label_Progress.Text = "已完成：" + String.Format("{0:F2}", Progress) + "%";
                    //this.Text = "已完成：" + String.Format("{0:F}", Progress) + "%" + " [" + FormatFileSize((big - small) * 1024) + "/s]";  这一句好像没有用
                }
                catch(Exception)
                {
                    try
                    {
                        label5.Text = "[总时长：NULL]";  //总时长的标签先设置为空
                        Double Downloaded = Convert.ToDouble(Convert.ToDouble(label6.Text.Substring(5, 2)) * 60 * 60 + Convert.ToDouble(label6.Text.Substring(8, 2)) * 60
                    + Convert.ToDouble(label6.Text.Substring(11, 2)) + Convert.ToDouble(label6.Text.Substring(14, 2)) / 100);
                        Double Progress = 100;

                        if (Progress > 100)  //防止进度条超过百分之百
                            Progress = 100;
                        if (Progress < 0)  //防止进度条小于零……
                            Progress = 0;

                        ProgressBar.Value = Convert.ToInt32(Progress);
                        //windowsTaskbar.SetProgressValue(Convert.ToInt32(Progress), 100, this.Handle);
                        label_Progress.Visible = true;
                        label_Progress.Text = "已完成：" + String.Format("{0:F}", Progress) + "%";
                        //this.Text = "已完成：" + String.Format("{0:F}", Progress) + "%" + " [" + FormatFileSize((big - small) * 1024) + "/s]";
                    }
                    catch (Exception) { }
                }
            }
            
        }

        //程序一加载后 进行的操作：其中之一： 设置当前下载路径 并显示在文本框中
        private void Form1_Load(object sender, EventArgs e)
        {
            ////初始化进度条 下面的这两行代码也是这样 注释掉后任务栏的就不能显示进度了
            //windowsTaskbar.SetProgressState(TaskbarProgressBarState.Normal, this.Handle);
            //windowsTaskbar.SetProgressValue(0, 100, this.Handle);

            if (!File.Exists(@"Tools\ffmpeg.exe"))  //判断程序目录有无ffmpeg.exe
            {
                MessageBox.Show("没有找到Tools\\ffmpeg.exe", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                //.net 有自动回收机制 对象新建后就不用释放了 close是关闭窗体  dispose是释放此类资源
                //close方法是与Close事件配合 dispose可以在我们需要删除这个窗体时进行调用
                //Dispose 类似于Close 方法
                Dispose();
                Application.Exit();
            }
            //若有配置文件：在配置文件里面可以更改下载的路径
            //Environment.GetFolderPath(Environment.SpecialFolder.System)   输出结果：C:\WINNT\System32
            //System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments) 输出结果：C:\Users\Administrator\Documents
            //也就是一打开，这个程序就会在这个目录下面进行寻找 如果找不到才进入下面的else
            //通过测试发现，在找不到这个目录的前提下 关闭程序 ， 会自动在该目录下生成setting.xml文件 且里面存储的地址就是上一下设置的地址 并且会保存上一次设置的文件后缀格式等等
            if (File.Exists(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments) + "\\M3u8_Downloader_Settings.xml"))  //判断程序目录有无配置文件，并读取文件
            {
                //MessageBox.Show("有配置文件");
                XmlDocument doc = new XmlDocument();
                doc.Load(@System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments) + "\\M3u8_Downloader_Settings.xml");    //加载Xml文件  
                XmlNodeList topM = doc.SelectNodes("Settings");
                foreach (XmlElement element in topM)
                {
                    textBox_DownloadPath.Text = element.GetElementsByTagName("DownPath")[0].InnerText;  //得到下载路径
                    //得到文件后缀 这样下一次打开的时候 就可以设置默认的格式了
                    if (element.GetElementsByTagName("ExtendName")[0].InnerText == "MP4") { radioButton1.Checked = true; }
                    if (element.GetElementsByTagName("ExtendName")[0].InnerText == "MKV") { radioButton2.Checked = true; }
                    if (element.GetElementsByTagName("ExtendName")[0].InnerText == "TS") { radioButton3.Checked = true; }
                    if (element.GetElementsByTagName("ExtendName")[0].InnerText == "FLV") { radioButton4.Checked = true; }
                }
            }
            else  //若无配置文件，获取当前程序运行路径，即为默认下载路径
            {
                string lujing = System.Environment.CurrentDirectory;   //得到当前环境路径 注意 这个与上面的又不一样.
                //MessageBox.Show("123");
                textBox_DownloadPath.Text = lujing;
            }
        }

        private void textBox_Adress_KeyPress(object sender, KeyPressEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox == null)
                return;
            if (e.KeyChar == (char)1)       // Ctrl-A 相当于输入了AscII=1的控制字符
            {
                textBox.SelectAll();
                e.Handled = true;      // 不再发出“噔”的声音
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void button_Exit_Click(object sender, EventArgs e)
        {
            SaveSettings();
            try
            {
                if (Process.GetProcessById(ffmpegid) != null)
                {
                    if (MessageBox.Show("已启动下载进程，确认退出吗？\n（这有可能是强制的）", "请确认您的操作", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == System.Windows.Forms.DialogResult.Yes)
                    {
                        Stop();
                        MessageBox.Show("已经发送命令！\n若进程仍然存在则强制结束！", "请确认"); 
                        try
                        {
                            if (Process.GetProcessById(ffmpegid) != null)  //如果进程还存在就强制结束它
                            {
                                Process.GetProcessById(ffmpegid).Kill();
                                Dispose();
                                Application.Exit();
                            }
                        }
                        catch
                        {
                            Dispose();
                            Application.Exit();
                        }
                                
                    }
                    else
                    {
                    }
                }
            }
            catch {
                Dispose();
                Application.Exit();
            }
        }

        

        private void Form1_MouseDown(object sender, MouseEventArgs e)
        {
            MoveFrom();
        }

        private void label8_MouseDown(object sender, MouseEventArgs e)
        {
            MoveFrom();
        }

        private void label14_Click(object sender, EventArgs e)
        {
            Process.Start("https://ffmpeg.zeranoe.com/builds/win32/static/");
        }

        private void label_About_Click(object sender, EventArgs e)
        {
            MessageBox.Show("nilaoda 编译于 2016/10/22\nCopyright ©  2016", "关于", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void Label_Monitor_Click(object sender, EventArgs e)
        {
            Exist_Run(@"Tools\HttpFileMonitor.exe");
        }

        private void Label_WriteLog_Click(object sender, EventArgs e)
        {
            String LogName = "日志-" + System.DateTime.Now.ToString("yyyy.MM.dd-HH.mm.ss") + ".txt";
            StreamWriter log = new StreamWriter(LogName);
            log.WriteLine("━━━━━━━━━━━━━━\r\n"
                + "■M3U8 Downloader 用户日志\r\n\r\n"
                + "■" + System.DateTime.Now.ToString("F") + "\r\n\r\n"
                + "■输入：" + textBox_Adress.Text + "\r\n\r\n"
                + "■输出：" + textBox_DownloadPath.Text + "\\" + textBox_Name.Text + houzhui.Text + "\r\n\r\n"
                + "■FFmpeg命令：ffmpeg " + Command.Text + "\r\n"
                + "━━━━━━━━━━━━━━"
                + "\r\n\r\n"
                + textBox_Info.Text);
            log.Close();
            MessageBox.Show("日志已生成到程序目录！", "提示信息", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void label_Update_Click(object sender, EventArgs e)
        {
            Process.Start("http://pan.baidu.com/s/1dF4uDuL");
        }

        private void label_OpenTool_Click(object sender, EventArgs e)
        {
            Exist_Run(@"Tools\Batch Download.exe");
        }

        private void label_Progress_MouseDown(object sender, MouseEventArgs e)
        {
            MoveFrom();
        }

        //计时器控件运行 ： 只有绑定了 Tick事件和设置 enable = true 才会自动计时 所以这里可以进行自动计时的效果

        //这个计时器主要用来计算文件大小
        private void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                //size=    1981kB time=00:00:35.08
                //这个 textBox_forRegex 就是正上方的白色框框 这个主要作用： 用来显示当前ffmpeg的输出信息  只是我们设置了visable = false 导致看不到了
                Regex size = new Regex(@"[1-9][0-9]{0,}kB time", RegexOptions.Compiled | RegexOptions.Singleline);//取已下载大小
                var sizekb = size.Matches(textBox_forRegex.Text);     //所以这里就可以直接在这里进行匹配了 不用再 textBox_info里面进行匹配了
                big = Convert.ToDouble(sizekb.OfType<Match>().Last().ToString().Replace("kB time", ""));
                label8.Text = "[" + FormatFileSize((big - small) * 1024) + "/s]";    //对于small tick中定义的是small= big 而计时器的间隔是1000毫秒 也就是说 本次的 small等于上一次的big
                //同样的 这里 FormatFileSize 是以B 为单位 所以原来的KB 需要*1024 -》 B 然后系统自动帮你转换
            }
            catch (Exception) { }
        }
        //计时器控件运行 ： 只有绑定了 Tick事件和设置 enable = true 才会自动及时
        private void timer2_Tick(object sender, EventArgs e)
        {
            small = big;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveSettings();
            try
            {
                if (Process.GetProcessById(ffmpegid) != null)
                {
                    if (MessageBox.Show("已启动下载进程，确认退出吗？\n（这有可能是强制的）", "请确认您的操作", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == System.Windows.Forms.DialogResult.Yes)
                    {
                        Stop();
                        MessageBox.Show("已经发送命令！\n若进程仍然存在则强制结束！", "请确认");
                        try
                        {
                            if (Process.GetProcessById(ffmpegid) != null)  //如果进程还存在就强制结束它
                            {
                                Process.GetProcessById(ffmpegid).Kill();
                                Dispose();
                                Application.Exit();
                            }
                        }
                        catch
                        {
                            Dispose();
                            Application.Exit();
                        }

                    }
                    else
                    {
                        e.Cancel=true;
                    }
                }
            }
            catch
            {
                Dispose();
                Application.Exit();
            }
        }

        private void textBox_Adress_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_DownloadPath_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox_Name_TextChanged(object sender, EventArgs e)
        {

        }

        private void ProgressBar_Click(object sender, EventArgs e)
        {

        }

        private void textBox_forRegex_TextChanged(object sender, EventArgs e)
        {

        }

        private void label8_Click(object sender, EventArgs e)
        {

        }

        private void timer3_Tick(object sender, EventArgs e)
        {

        }
    }
    }

//不知道这里为什么不写在一起
namespace M3U8_Downloader
{
    //ProgressBar 就是进度条 但是C# 自带的不能显示进度的百分比 ，不能设置前景和背景色
    class MyProgressBar : ProgressBar //新建一个MyProgressBar类，它继承了ProgressBar的所有属性与方法
    {
        public MyProgressBar()
        {
            base.SetStyle(ControlStyles.UserPaint, true);//使控件可由用户自由重绘
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            SolidBrush brush = null;
            Rectangle bounds = new Rectangle(0, 0, base.Width, base.Height);
            e.Graphics.FillRectangle(new SolidBrush(this.BackColor), 1, 1, bounds.Width - 2, bounds.Height - 2);//此处完成背景重绘，并且按照属性中的BackColor设置背景色
            bounds.Height -= 4;
            bounds.Width = ((int)(bounds.Width * (((double)base.Value) / ((double)base.Maximum)))) - 4;//是的进度条跟着ProgressBar.Value值变化
            brush = new SolidBrush(this.ForeColor);
            e.Graphics.FillRectangle(brush, 2, 2, bounds.Width, bounds.Height);//此处完成前景重绘，依旧按照Progressbar的属性设置前景色
        }
    }

    // 1.定义委托  
    public delegate void DelReadStdOutput(string result);
    public delegate void DelReadErrOutput(string result);

    public partial class Form1 : Form
    {
        // 2.定义委托事件  
        public event DelReadStdOutput ReadStdOutput;
        public event DelReadErrOutput ReadErrOutput;

        //原来的程序把这个是写在上面的 这里挪到这里 效果是一样的  因为网上的代码就是这样写的
        public Form1()
        {
            InitializeComponent();  //系统添加
            Init();  //作者添加的：初始化的时候就应该将相应的函数注册到委托事件中
            Control.CheckForIllegalCrossThreadCalls = false;  //禁止编译器对跨线程访问做检查 网上的代码没有写这一行
        }
        private void Init()
        {
            //3.将相应函数注册到委托事件中  
            ReadStdOutput += new DelReadStdOutput(ReadStdOutputAction);
            ReadErrOutput += new DelReadErrOutput(ReadErrOutputAction);
        }

        private void button_Download_Click(object sender, EventArgs e)
        {
            if (!Directory.Exists(textBox_DownloadPath.Text))//若文件夹不存在则新建文件夹    这里加不加this效果是一样的
            {
                Directory.CreateDirectory(textBox_DownloadPath.Text); //新建文件夹  
               
               
            }  

            else
            {
                textBox_Info.Text = "";             //右边的下载文件框显示为空
                textBox_forRegex.Text = "";         //右上方白色进度条显示为空
                Download();
                //以下标签都是默认不进行显示的
                linkLabel_Stop.Visible = true;      //在没有下载的时候 信息显示旁边的点此终止是隐藏的 只有在下载的时候才可以进行显示
                label5.Visible = true;              //总时长显示
                label6.Visible = true;              //已下载显示
                label7.Visible = true;              //视频信息显示
                label8.Visible = true;              //网速显示
                timer1.Enabled = true;              //设置计时器运行   点击下载的时候 计时器就开始工作了
                timer2.Enabled = true;              //设置计时器运行
            }
            
        }
        private void Download()   //看看选择的是哪个格式 将下载的文件后缀改为那个格式
        {
            if (radioButton1.Checked == true)  //文件格式是mp4
            {
                houzhui.Text = ".mp4";      //这玩意儿隐藏在请在左侧进行操作这个标签的下面, 拖动这个标签就可以找得到了 也是一个标签
                //这个command文本框也是隐藏在下面
                //默认里面是写的.*
                //这里是写的ffmpeg 命令： ffmpeg -threads 0 -i ***.m3u8 -c copy -y -bsf:a aac_adtstoasc -movflags +faststart ***.mp4
                //
                Command.Text = "-threads 0 -i " + "\"" + textBox_Adress.Text + "\"" + " -c copy -y -bsf:a aac_adtstoasc -movflags +faststart " + "\"" + textBox_DownloadPath.Text + "\\" + textBox_Name.Text + ".mp4" + "\"";
                // 启动进程执行相应命令,此例中以执行ffmpeg.exe为例  
                RealAction(@"Tools\ffmpeg.exe", Command.Text);
            }
            if (radioButton2.Checked == true)  //文件格式是mkv
            {
                houzhui.Text = ".mkv";
                Command.Text = "-threads 0 -i " + "\"" + textBox_Adress.Text + "\"" + " -c copy -y -bsf:a aac_adtstoasc " + "\"" + textBox_DownloadPath.Text + "\\" + textBox_Name.Text + ".mkv" + "\"";
                RealAction(@"Tools\ffmpeg.exe", Command.Text);
            }
            if (radioButton3.Checked == true)    //文件格式是ts
            {
                houzhui.Text = ".ts";
                Command.Text = "-threads 0 -i " + "\"" + textBox_Adress.Text + "\"" + " -c copy -y -f mpegts " + "\"" + textBox_DownloadPath.Text + "\\" + textBox_Name.Text + ".ts" + "\"";
                RealAction(@"Tools\ffmpeg.exe", Command.Text);
            }
            if (radioButton4.Checked == true)    //文件格式是flv
            {
                houzhui.Text = ".flv";
                Command.Text = "-threads 0 -i " + "\"" + textBox_Adress.Text + "\"" + " -c copy -y -f f4v -bsf:a aac_adtstoasc " + "\"" + textBox_DownloadPath.Text + "\\" + textBox_Name.Text + ".flv" + "\"";
                RealAction(@"Tools\ffmpeg.exe", Command.Text);
            }
        }
        private void RealAction(string StartFileName, string StartFileArg)
        {
            //string str = "ipconfig";
            //Process cmdProcess = new Process();
            //cmdProcess.StartInfo.FileName = "cmd.exe";
            //cmdProcess.StartInfo.UseShellExecute = false;
            //cmdProcess.StartInfo.RedirectStandardInput = true;
            //cmdProcess.StartInfo.RedirectStandardOutput = true;
            ////cmdProcess.StartInfo.RedirectStandardError = true;
            ////cmdProcess.StartInfo.CreateNoWindow = true;

            //cmdProcess.StandardInput.WriteLine(str + "&exit");
            //cmdProcess.StandardInput.AutoFlush = true;
            //string output = cmdProcess.StandardOutput.ReadToEnd();
            //cmdProcess.WaitForExit();
            //cmdProcess.Close();
            //Console.WriteLine(output);



            Process CmdProcess = new Process();     //创建一个新的进程
            CmdProcess.StartInfo.FileName = StartFileName;      // 命令  : 这里可以看成是启动了这个程序
            CmdProcess.StartInfo.Arguments = StartFileArg;      // 参数   这里可以看成是 给这个程序所传递的参数

            CmdProcess.StartInfo.CreateNoWindow = true;         // 不创建新窗口   这里如果写false 那么会新打开一个DOS窗口
            CmdProcess.StartInfo.UseShellExecute = false;       //是否使用操作系统shell调用
            CmdProcess.StartInfo.RedirectStandardInput = true;  // 重定向输入 ： 接受来自调用程序的输入信息
            CmdProcess.StartInfo.RedirectStandardOutput = true; // 重定向标准输出:由调用程序获取输出信息   这里也就是说会在文本框中输出调用ffmpeg输出的结果 
            CmdProcess.StartInfo.RedirectStandardError = true;  // 重定向错误输出  
            //CmdProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;  

            CmdProcess.OutputDataReceived += new DataReceivedEventHandler(p_OutputDataReceived);  //调用下面的函数
            CmdProcess.ErrorDataReceived += new DataReceivedEventHandler(p_ErrorDataReceived);    //调用下面的函数

            CmdProcess.EnableRaisingEvents = true;                      // 启用Exited事件  既是执行函数：CmdProcess_Exited
            CmdProcess.Exited += new EventHandler(CmdProcess_Exited);   // 注册进程结束事件  这里是否可以看成是进行了异步处理？

            CmdProcess.Start();
            ffmpegid = CmdProcess.Id;                     //获取ffmpeg.exe的进程
            CmdProcess.BeginOutputReadLine();             //这一句是为上面的CmdProcess.StartInfo.RedirectStandardOutput 服务的  如果上面设置的是false 那么这里调用这个函数将会报错 
            CmdProcess.BeginErrorReadLine();               //这一行代码不能省略 如果省略 则在文本框里面不会显示了  起作用的实际上是这一句 不是上一句                            
            //上面是按行读入的 下面也要按行读入进行显示
            // 如果打开注释，则以同步方式执行命令，此例子中用Exited事件异步执行。  
            // CmdProcess.WaitForExit();       

        }

        //以下为实现异步输出CMD信息



        private void p_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                // 4. 异步调用，需要invoke  
                this.Invoke(ReadStdOutput, new object[] { e.Data });
            }
        }

        private void p_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                //4. 异步调用 需要invoke 
                this.Invoke(ReadErrOutput, new object[] { e.Data });
            }
        }

        private void ReadStdOutputAction(string result)    //这个和下一个都是 将ffmpeg 的输出信息都出来
        {
            //MessageBox.Show(result);
            textBox_forRegex.Text = result;   //先覆盖掉默认的那几个字：请在左侧进行操作。
            this.textBox_Info.AppendText(result + "\r\n");   //然后在末尾进行添加一行 因为每次也是读一行的
        }

        private void ReadErrOutputAction(string result)  //这里同上
        {
            //MessageBox.Show(result);
            textBox_forRegex.Text = result;
            this.textBox_Info.AppendText(result + "\r\n");
        }

        //执行结束后触发 的事件
        private void CmdProcess_Exited(object sender, EventArgs e)
        {

            FlashWindow(this.Handle, true);

            //设置任务栏进度条状态
            windowsTaskbar.SetProgressState(TaskbarProgressBarState.NoProgress, this.Handle);
            // 感觉这一句好像没有用
            //this.Text = "M3U8 Downloader";   
            //
            this.label_Progress.Text = "已完成：" + "100.00%";
            ProgressBar.Value = 100;      //进度条的值
            timer1.Enabled = false;        //计时器暂停运行
            timer2.Enabled = false;         //计时器暂停运行
            label8.Text = "";  //网速显示
            
            //弹出来一个框
            MessageBox.Show("命令执行结束！", "M3U8 Downloader", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, MessageBoxOptions.DefaultDesktopOnly);  // 执行结束后触发
        }

        //退出运行
        private void Exist_Run(string FileName)
        {
            if (File.Exists(FileName))  //判断有无某文件
            {
                Process.Start(FileName);
            }
            else
            {
                MessageBox.Show("没有找到" + FileName, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //移动窗口
        private void MoveFrom()
        {
            ReleaseCapture();   //可以移动窗口
            SendMessage(this.Handle, WM_SYSCOMMAND, SC_MOVE + HTCAPTION, 0);
        }

        //退出来的时候 把本次的设置保存在xml中
        private void SaveSettings()  
        {
            string ExtendName = "";
            if (radioButton1.Checked == true) { ExtendName = "MP4"; }
            if (radioButton2.Checked == true) { ExtendName = "MKV"; }
            if (radioButton3.Checked == true) { ExtendName = "TS"; }
            if (radioButton4.Checked == true) { ExtendName = "FLV"; }

           //在相应的文件目录下面查找 这里是如果有原文件 那么删除后再添加 类似于write 
            XmlTextWriter xml = new XmlTextWriter(@System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments) + "\\M3u8_Downloader_Settings.xml", Encoding.UTF8);
            xml.Formatting = Formatting.Indented;
            xml.WriteStartDocument();
            xml.WriteStartElement("Settings");
            
            xml.WriteStartElement("DownPath"); xml.WriteCData(textBox_DownloadPath.Text); xml.WriteEndElement();
            xml.WriteStartElement("ExtendName"); xml.WriteCData(ExtendName); xml.WriteEndElement();

            xml.WriteEndElement();
            xml.WriteEndDocument();
            xml.Flush();
            xml.Close();
            
        }



        //这里传递的参数是： startFileName = @"Tools\ffmpeg.exe"  startFileArg =  Command.Text


        public void Stop()
        {
            AttachConsole(ffmpegid);
            SetConsoleCtrlHandler(IntPtr.Zero, true);
            GenerateConsoleCtrlEvent(0, 0);
            FreeConsole();
        }

    }
}