using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace TestRunner
{
    public partial class Form1 : Form
    {

        public Form1()
        {
            InitializeComponent();

            folderBrowserDialog1.SelectedPath = txtFeaturesPath.Text;
            folderBrowserDialog2.SelectedPath = txtProjectPath.Text;
            BindTree();

            //不检查线程
            CheckForIllegalCrossThreadCalls = false;
        }

        private void btnFeaturesFolder_Click(object sender, EventArgs e)
        {
            var result = folderBrowserDialog1.ShowDialog();
            if (result.ToString() == "OK")
            {
                txtFeaturesPath.Text = folderBrowserDialog1.SelectedPath;
            }
        }

        private void txtFolderPath_TextChanged(object sender, EventArgs e)
        {
            BindTree();
        }

        private void BindTree()
        {
            string dir = folderBrowserDialog1.SelectedPath;
            if (string.IsNullOrWhiteSpace(dir))
                return;

            treeView1.Nodes.Clear();
            var node = treeView1.Nodes.Add(dir);
            AddFolder(dir, node);

            if (treeView1.Nodes.Count > 0)
                treeView1.Nodes[0].Expand();
        }

        private void AddFolder(string folderPath, TreeNode parentNode)
        {
            DirectoryInfo di = new DirectoryInfo(folderPath);
            var fis = di.GetFiles();
            foreach (var fi in fis)
            {
                if (fi.Extension.ToLower().Trim() == ".feature")
                    parentNode.Nodes.Add(fi.FullName);
            }
            var fds = di.GetDirectories();
            foreach (var fd in fds)
            {
                var node = parentNode.Nodes.Add(fd.FullName);

                AddFolder(fd.FullName, node);
            }
        }

        private void treeView1_AfterCheck(object sender, TreeViewEventArgs e)
        {
            CheckChildren(e.Node);

            if (e.Node.Text.Trim().ToLower().EndsWith(".feature"))
            {
                if (e.Node.Checked)
                {
                    if (!lbxSort.Items.Contains(e.Node.Text))
                        lbxSort.Items.Insert(lbxSort.Items.Count, e.Node.Text);
                }
                else
                {
                    if (lbxSort.Items.Contains(e.Node.Text))
                        lbxSort.Items.Remove(e.Node.Text);
                }
            }

            treeView1.Enabled = true;
        }


        private void CheckChildren(TreeNode parentNode)
        {
            if (parentNode.Nodes.Count > 0)
            {
                foreach (TreeNode child in parentNode.Nodes)
                {
                    child.Checked = parentNode.Checked;
                    CheckChildren(child);
                }
            }
        }

        private void treeView1_BeforeCheck(object sender, TreeViewCancelEventArgs e)
        {
            treeView1.Enabled = false;
        }

        private void btnUp_Click(object sender, EventArgs e)
        {
            int index = lbxSort.SelectedIndex;
            if (index > 0)
            {
                for (int i = 0; i < lbxSort.SelectedItems.Count; i++)
                {
                    var tmp = lbxSort.SelectedItems[i];
                    lbxSort.Items.Remove(lbxSort.SelectedItems[i]);
                    lbxSort.Items.Insert(index - 1, tmp);
                    lbxSort.SetSelected(index - 1, true);
                }
            }

        }

        private void btnDown_Click(object sender, EventArgs e)
        {
            int index = lbxSort.SelectedIndex;
            if (index < lbxSort.Items.Count - 1)
            {
                for (int i = 0; i < lbxSort.SelectedItems.Count; i++)
                {
                    var tmp = lbxSort.SelectedItems[i];
                    lbxSort.Items.Remove(lbxSort.SelectedItems[i]);
                    lbxSort.Items.Insert(index + 1, tmp);
                    lbxSort.SetSelected(index + 1, true);
                }
            }
        }

        private void btnRun_Click(object sender, EventArgs e)
        {
            DateTime st = DateTime.Now;
            try
            {

                if (string.IsNullOrWhiteSpace(txtProjectPath.Text))
                    return;
                btnRun.Enabled = false;
                txtResults.SelectionFont = new Font("Microsoft Sans Serif", 9, FontStyle.Bold);
                txtResults.SelectionColor = Color.Black;
                txtResults.AppendText(DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss ") + "Running Start.");
                txtResults.AppendText(Environment.NewLine);

                //读取配置类
                string configPath = Path.Combine(txtProjectPath.Text, @"src\test\java\CucumberTest\TestRunner.java");
                string configText = File.ReadAllText(configPath);

                foreach (var item in lbxSort.Items)
                {
                    //改写配置类
                    string path = item.ToString();
                    if (!path.ToLower().EndsWith(".feature"))
                        continue;
                    using (var feature = File.Open(path, FileMode.Open, FileAccess.Read))
                    {
                        using (StreamReader rd = new StreamReader(feature))
                        {
                            for (; ; )
                            {
                                string s = rd.ReadLine().Trim();
                                if (s.StartsWith("@"))
                                {
                                    string newConfig = Regex.Replace(configText, @"""@[^ ""]*""", '"' + s + '"');
                                    File.WriteAllText(configPath, newConfig);
                                    break;
                                }
                            }
                        }
                    }
                    //调用maven跑用例
                    string batName = "mavenRun.bat";
                    var fs = File.CreateText(batName);

                    fs.WriteLine(txtProjectPath.Text.Trim().Substring(0, 2));
                    fs.WriteLine(string.Format(@"cd ""{0}""", txtProjectPath.Text));
                    fs.WriteLine("mvn test");
                    fs.WriteLine("pause");
                    fs.Close();
                    var pcs = Process.Start(new ProcessStartInfo() { FileName = batName, Verb = "runas" });

                    txtResults.SelectionFont = new Font("Microsoft Sans Serif", 9, FontStyle.Bold);
                    txtResults.SelectionColor = Color.RoyalBlue;
                    txtResults.AppendText(DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss ") + "Running : " + item);
                    txtResults.AppendText(Environment.NewLine);

                    var so = DateTime.Now;

                    pcs.WaitForExit();

                    txtResults.SelectionFont = new Font("Microsoft Sans Serif", 9, FontStyle.Bold);
                    txtResults.SelectionColor = Color.RoyalBlue;
                    txtResults.AppendText(DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss ") + "Done.");
                    txtResults.AppendText("Cost time : " + (DateTime.Now - so).TotalSeconds + " sec");
                    txtResults.AppendText(Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                txtResults.SelectionFont = new Font("Microsoft Sans Serif", 9, FontStyle.Bold);
                txtResults.SelectionColor = Color.Red;
                txtResults.AppendText(DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss ") + " " + ex.Message);
                txtResults.AppendText(Environment.NewLine);
            }
            finally
            {

                txtResults.SelectionFont = new Font("Microsoft Sans Serif", 9, FontStyle.Bold);
                txtResults.SelectionColor = Color.Black;
                txtResults.AppendText(DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss ") + "Running finish.");
                txtResults.AppendText("Cost time : " + (DateTime.Now - st).TotalMinutes + " min");
                txtResults.AppendText(Environment.NewLine);
                btnRun.Enabled = true;
            }
        }

        private void btnProjectPath_Click(object sender, EventArgs e)
        {
            var result = folderBrowserDialog2.ShowDialog();
            if (result.ToString() == "OK")
            {
                txtProjectPath.Text = folderBrowserDialog2.SelectedPath;
            }
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Test Runner, batch testing tool, made by John Yue, 2018.");
        }
    }
}
