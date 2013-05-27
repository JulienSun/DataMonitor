using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Data.SqlClient;
using System.Data;
using System.Threading;

using System.Net;
using System.Net.Sockets;

using System.Windows.Threading;

using System.Diagnostics;

using System.Collections.ObjectModel;

using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.DataSources;

using System.Xml;
using System.IO;

namespace DataMonitorCenter
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        // 接收gprs的线程对象，接收消息完成的标志状态
        public Thread receiveGprsThread;
        public static bool messageReceived = false;
  
        // 曲线显示的数据源
        private ObservableDataSource<Point> dataSource = new ObservableDataSource<Point>();

        // 曲线
        private LineGraph graphTemperature = new LineGraph();

        // 数据库
        public static DatabaseTool dbTool;

        
        // 今天的时间，用于判断是否动态显示
        private string nowText;

        // 当前显示的节点
        private int node;

         // 异步接收
         IPEndPoint e ;
         UdpClient u ;

         UdpState s;
            

        public MainWindow()
        {
            InitializeComponent();

            // 设置当前线程的名称
            Thread.CurrentThread.Name = "MainWindow";

            

            this.DataContext = this;

            
            // 初始化数据库连接
            dbTool = new DatabaseTool("monitor", "sa", "sa", "localhost");

            nowTextBlock.Text = DateTime.Now.ToShortDateString();
            nowText = nowTextBlock.Text;

            // 初始化节点树
            initTree();

            // 异步接收
            e = new IPEndPoint(IPAddress.Any, 5999);
            u = new UdpClient(e);

            s = new UdpState();
            s.e = e;
            s.u = u;
            
            
        }

       

        // 线程函数：接收GPRS的数据，并且存储到数据库
        public void reveiveGprsFun()
        {
            // 设置线程名称
            Thread.CurrentThread.Name = "receiveGprsFun";

           

            while (true)
            {
                try
                {
                    MainWindow.messageReceived = false;
                    u.BeginReceive(new AsyncCallback(ReceiveCallback), s);
                    while (!MainWindow.messageReceived)
                    {
                        Thread.Sleep(100);
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
              
            }
        }

        // udp接收完成回调函数
        private void ReceiveCallback(IAsyncResult ar)
        {
            MainWindow.messageReceived = true;

            UdpClient u = (UdpClient)((UdpState)(ar.AsyncState)).u;
            IPEndPoint e = (IPEndPoint)((UdpState)(ar.AsyncState)).e;

            Byte[] receiveBytes = u.EndReceive(ar, ref e);
            string receiveString = Encoding.ASCII.GetString(receiveBytes);

            string[] splitStrings = receiveString.Split('a');
            int node = int.Parse(splitStrings[0]);
            float temperature = float.Parse(splitStrings[1]);

            string strIp = e.Address.ToString();

            string time = DateTime.Now.ToShortDateString();

            Console.WriteLine("{0} {1} {2} {3}", node, strIp, temperature, time);

            int safe = 1;
            if (temperature > 58)
            {
                safe = 0;
            }
            else
            {
                safe = 1;
            }

            string sql = @"insert into node" + node.ToString() + @" (temperature, ip, date, time, safe) Values('" + temperature + "','" + strIp + "','" + time + "','" + time + "','" + safe + "')";
            MainWindow.dbTool.insertSql(sql);        

            // 判断是否需要动态显示;如果接收到的节点编号和当前显示的节点相同，则需要动态显示
            if (time.Equals(nowText) && node == this.node)
            {
                int i = dataSource.Collection.Count();
                dataSource.AppendAsync(base.Dispatcher, new Point(i + 1, temperature));
            }
        }

        // 窗口关闭
        private void Window_Closed(object sender, EventArgs e)
        {
            if (receiveGprsThread != null)
            {
                if(receiveGprsThread.IsAlive)
                    receiveGprsThread.Abort();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            graphTemperature = plotter.AddLineGraph(dataSource, Colors.Green, 2, "结点1");
        
            plotter.Viewport.FitToView();
        }

      
       
        
        // 查询
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            string strDate = date.Text.ToString();
            nowTextBlock.Text = strDate;
            nowText = strDate;

            DataTable dataTable = new DataTable();
            dbTool.getNodeTeperature(strDate, this.node, out dataTable);

            // 将dataTable中的temperature加入到datasource
            plotter.Children.Remove(graphTemperature);
            dataSource = new ObservableDataSource<Point>();
            graphTemperature = plotter.AddLineGraph(dataSource, Colors.Green, 2, "结点"+this.node.ToString());
            for (int i = 0; i < dataTable.Rows.Count; i++)
            {
                float temperature = float.Parse(dataTable.Rows[i].ItemArray[1].ToString());
                //Console.WriteLine("{0}", temperature);
                dataSource.AppendAsync(base.Dispatcher, new Point(i, temperature));
            }
        }

        // 添加节点
        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            WindowAddNode winAddNode = new WindowAddNode();
            winAddNode.ShowDialog();
            
            initTree();
            

        }

        // 修改节点
        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {
            WindowNodeUpdate winNodeUpdate = new WindowNodeUpdate();
            winNodeUpdate.ShowDialog();

        }

        // 删除节点
        private void MenuItem_Click_2(object sender, RoutedEventArgs e)
        {
            WindowDeleteNode winDeleteNode = new WindowDeleteNode();
            winDeleteNode.ShowDialog();
            
            initTree();
            
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            TreeView treeView = (TreeView)e.Source;
            TreeViewItem item = (TreeViewItem)treeView.SelectedItem;

            

            String strNode = item.Header.ToString();
            int node = strNode[strNode.Length - 1] - 48;
            if (strNode.Equals("结点" + node.ToString()))
            {
                

                this.node = node;
                image1.Visibility = Visibility.Hidden;
                tab.Visibility = Visibility.Visible;
                tab.SelectedIndex = 0;
                lineDisplay(node);
            }

            
        }

        public void initTree()
        {

            string strSql = "select bianhao from nodeinfo order by bianhao";
            DataTable dataTable = new DataTable();
            App.DbTool.selectSql(strSql, out dataTable);

            this.treeNode.Items.Clear();

            for (int i = 0; i < dataTable.Rows.Count; i++)
            {
                string bianhao = dataTable.Rows[i].ItemArray[0].ToString();
                TreeViewItem item = new TreeViewItem();
                item.Header = "结点" + bianhao;
                this.treeNode.Items.Add(item);

            }
        }

        public void lineDisplay(int node)
        {
            nowTextBlock.Text = DateTime.Now.ToShortDateString();

            DataTable dataTable = new DataTable();
            dbTool.getNodeTeperature(DateTime.Now.ToShortDateString(), node, out dataTable);

            // 将dataTable中的temperature加入到datasource
            plotter.Children.Remove(graphTemperature);
            dataSource = new ObservableDataSource<Point>();
            graphTemperature = plotter.AddLineGraph(dataSource, Colors.Green, 2, "结点"+node.ToString());
            for (int i = 0; i < dataTable.Rows.Count; i++)
            {
                float temperature = float.Parse(dataTable.Rows[i].ItemArray[1].ToString());
                //Console.WriteLine("{0}", temperature);
                dataSource.AppendAsync(base.Dispatcher, new Point(i, temperature));
            }
        }

       

        private void tab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //MessageBox.Show(e.Source.ToString());
            TabControl tabControl = e.OriginalSource as TabControl;

            if (tabControl != null)
            {
                int selectedIndex = tabControl.SelectedIndex;
                switch (selectedIndex)
                {
                    case 0:
                        Console.WriteLine("0");
                        break;
                    // 显示温度的数据
                    case 1:
                        Console.WriteLine("1");
                        dataDisplay();
                        break;
                    case 2:
                        Console.WriteLine("2");
                        terminalDisplay();
                        break;
                }
            }

            e.Handled = true;
        }

        private void dataDisplay()
        {
            string strSql = "select * from node" + this.node.ToString();
            DataTable dataTable = new DataTable();
            App.DbTool.selectSql(strSql, out dataTable);
            dataTable.Columns[0].ColumnName = "编号";
            dataTable.Columns[1].ColumnName = "温度（°）";
            dataTable.Columns[2].ColumnName = "IP地址";
            dataTable.Columns[3].ColumnName = "日期";
            dataTable.Columns[4].ColumnName = "时间";
            dataTable.Columns[5].ColumnName = "是否安全";
            wenduDataGrid.ItemsSource = dataTable.DefaultView;

            Console.WriteLine("lie:{0}", wenduDataGrid.Columns.Count);
            if (wenduDataGrid.Columns.Count == 6)
            {
                int wid = (int)this.Width - 100;
                Console.WriteLine("宽度{0}",this.Width);
               
                wenduDataGrid.Columns[0].Width = 50;
                wenduDataGrid.Columns[1].Width = 50;
                wenduDataGrid.Columns[2].Width = 80;
                wenduDataGrid.Columns[3].Width = 80;
                wenduDataGrid.Columns[4].Width = 80;
                wenduDataGrid.Columns[5].Width = 80;
                
            }
        }

        private void terminalDisplay()
        {
            string strSql = "select * from nodeinfo where bianhao="+this.node.ToString();
            DataTable dataTable = new DataTable();
            App.DbTool.selectSql(strSql, out dataTable);
            textBoxBianhao.Text = this.node.ToString();
            textBoxLoc.Text = dataTable.Rows[0].ItemArray[2].ToString();
            TextRange textRange = new TextRange(richTextBoxDes.Document.ContentStart, richTextBoxDes.Document.ContentEnd);
            textRange.Text = dataTable.Rows[0].ItemArray[3].ToString();

            strSql = "select max(temperature), min(temperature), avg(temperature) from node" + this.node.ToString();
            dataTable = new DataTable();
            App.DbTool.selectSql(strSql, out dataTable);
            textBoxMax.Text = dataTable.Rows[0].ItemArray[0].ToString();
            textBoxMin.Text = dataTable.Rows[0].ItemArray[1].ToString();
            textBoxAve.Text = dataTable.Rows[0].ItemArray[2].ToString();

            strSql = "select id, temperature from node" + this.node.ToString() + " where safe = 0";
            dataTable = new DataTable();
            App.DbTool.selectSql(strSql, out dataTable);
            Console.WriteLine(dataTable.Rows.Count.ToString());
            int lastid = -1;
            int id;
            int errors = 0;
            for (int i = 0; i < dataTable.Rows.Count; i++)
            {
                id = int.Parse(dataTable.Rows[i].ItemArray[0].ToString());
                if (id - lastid > 1)
                {
                    errors = errors + 1;
                }
                lastid = id;
            }
            textBoxTime.Text = errors.ToString();
        }

        // 开始监听
        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            Connect.IsEnabled = false;
            DisConnect.IsEnabled = true;

            // 接收数据
            receiveGprsThread = new Thread(new ThreadStart(reveiveGprsFun));
            receiveGprsThread.Start();
        }

        // 断开连接
        private void DisConnect_Click(object sender, RoutedEventArgs e)
        {
            Connect.IsEnabled = true;
            DisConnect.IsEnabled = false;

            receiveGprsThread.Abort();
        }

        private void ViewUser_Click(object sender, RoutedEventArgs e)
        {
            WindowViewUser winViewUser = new WindowViewUser();
            winViewUser.ShowDialog();
        }

        private void AddUser_Click(object sender, RoutedEventArgs e)
        {
            WindowAddUser winAddUser = new WindowAddUser();
            winAddUser.ShowDialog();
        }

        private void AlterUser_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("请在用户信息表里面进行修改操作");
            WindowViewUser winViewUser = new WindowViewUser();
            winViewUser.ShowDialog();
        }

        private void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("请在用户信息表里面进行删除操作");
            WindowViewUser winViewUser = new WindowViewUser();
            winViewUser.ShowDialog();
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            string name = userName.Text;
            string password = userPd.Password;

            string strSql = "select password from userinfo where name = " + "'" + name + "'";
            DataTable dataTable = new DataTable();
            dbTool.selectSql(strSql, out dataTable);
            if (dataTable.Rows.Count > 0)
            {
                string pdDatabase = dataTable.Rows[0].ItemArray[0].ToString();
                if (pdDatabase.Equals(password))
                {
                    allMenu.IsEnabled = true;
                    rootTree.IsEnabled = true;
                    Connect.IsEnabled = true;
                    login.Visibility = Visibility.Hidden;
                    welcome.Visibility = Visibility.Visible;
                    weluser.Text = "欢迎您 " + name;
                    MessageBox.Show("登录成功");
                }
                else
                {
                    MessageBox.Show("密码错误");

                }
            }
            else
            {
                MessageBox.Show("该用户名不存在");
            }
        }

      
     

      
       
    }

    class UdpState
    {
        public UdpClient u;
        public IPEndPoint e;
    }
}
