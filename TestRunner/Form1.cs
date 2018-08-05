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
        string projectPath = "";
        string featurePath
        {
            get { return projectPath + @"\src\test\java\resources"; }
        }

        string resultPath
        {
            get { return projectPath + @"\target\cucumber\report.js"; }
        }

        public Form1()
        {
            InitializeComponent();

            //不检查线程
            CheckForIllegalCrossThreadCalls = false;
        }

        private void btnProjectPath_Click(object sender, EventArgs e)
        {
            var result = folderBrowserDialog1.ShowDialog();
            if (result.ToString() == "OK")
            {
                txtProjectPath.Text = folderBrowserDialog1.SelectedPath;
                projectPath = folderBrowserDialog1.SelectedPath;
            }
        }


        private void txtProjectPath_TextChanged(object sender, EventArgs e)
        {
            projectPath = txtProjectPath.Text;
            BindTree();
        }


        private void BindTree()
        {
            if (string.IsNullOrWhiteSpace(featurePath))
                return;

            treeView1.Nodes.Clear();
            var node = treeView1.Nodes.Add(featurePath);
            AddFolder(featurePath, node);

            if (treeView1.Nodes.Count > 0)
                treeView1.Nodes[0].Expand();
        }

        private void AddFolder(string featurePath, TreeNode parentNode)
        {
            DirectoryInfo di = new DirectoryInfo(featurePath);
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

                    //检查结果
                    string jg = "";
                    long tm = 0;
                    if (!File.Exists(resultPath))
                        jg = "Failed";
                    else
                    {
                        string resultText = File.ReadAllText(resultPath);
                        if (resultText.Contains(@"""status"": ""skipped""") || resultText.Contains(@"""status"": ""failed"""))
                            jg = "Failed";
                        else if (resultText.Contains(@"""status"": ""passed"""))
                        {
                            jg = "Passed";
                            var mts = Regex.Matches(resultText, @"""duration"":([^,]*),");
                            foreach (var mt in mts)
                            {
                                string match = mt.ToString();
                                tm += Convert.ToInt64(match.Replace(@"""duration"":", "").Replace(",", ""));
                            }
                        }
                        else
                            jg = "Failed";
                    }

                    //显示结果
                    txtResults.SelectionFont = new Font("Microsoft Sans Serif", 9, FontStyle.Bold);
                    txtResults.SelectionColor = jg == "Passed" ? Color.Green : Color.Red;
                    txtResults.AppendText(DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss ") + jg);
                    //txtResults.AppendText(". Cost time : " + (DateTime.Now - so).TotalSeconds.ToString("F2") + " sec");
                    txtResults.AppendText(". Cost time : " + (tm / 1000000000).ToString("F2") + " sec");
                    txtResults.AppendText(Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                txtResults.SelectionFont = new Font("Microsoft Sans Serif", 9, FontStyle.Bold);
                txtResults.SelectionColor = Color.Orange;
                txtResults.AppendText(DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss ") + " " + ex.Message);
                txtResults.AppendText(Environment.NewLine);
            }
            finally
            {

                txtResults.SelectionFont = new Font("Microsoft Sans Serif", 9, FontStyle.Bold);
                txtResults.SelectionColor = Color.Black;
                txtResults.AppendText(DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss ") + "Running finish.");
                txtResults.AppendText("Cost time : " + (DateTime.Now - st).TotalMinutes.ToString("F2") + " min");
                txtResults.AppendText(Environment.NewLine);
                btnRun.Enabled = true;
            }
        }



        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Test Runner, batch testing tool, made by John Yue, 2018.");
        }

    }
}
