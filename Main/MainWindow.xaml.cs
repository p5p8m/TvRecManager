//***********************************************************************************************************
// Revision       $Revision: 26014 $
// Last Modified  $Date: 2015-06-01 16:55:35 +0200 (Mo, 01. Jun 2015) $
// Author         $Author: pascal.melix $
// File           $URL: https://csvnhou-pro.houston.hp.com:18490/svn/sa_paf-tsrd/storage/source/trunk/sanxpert/Code/gui/sanreporter/AttributeReportGenerator.cs $
//***********************************************************************************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using System.Text.RegularExpressions;
using IO = System.IO;
using Forms = System.Windows.Forms;
using Microsoft.WindowsAPICodePack.Dialogs;
using TvRecManager;

namespace Main
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            LeftRecordsLstBox.Tag = new TvRecordsList();
            RightRecordsLstBox.Tag = new TvRecordsList();
            UpdateStacksLabel();
        }
        public static DependencyProperty ProgressValProperty =
        DependencyProperty.Register("ProgressVal", typeof(string), typeof(MainWindow));
        public string ProgressVal
        {
            get { return (string)GetValue(ProgressValProperty); }
            set { SetValue(ProgressValProperty, value); }
        }

        private const string ExtM3UFmtMarker = "#EXTM3U";
        private const string ExtInfFmtMarker = "#EXTINF:";
        private const string ExtHd3OptFmtMarker = "#EXTHD3OPT:";
        private const string ExtHdPosFmtMarker = "/tpos:";
        private const string MasterFile = "VIDEO.m3u";
        private const string ExtM3ULineFmt = ExtInfFmtMarker + "{0},{1} _ {2}_{3}_{4}_{5}";
        private const string ExtM3UPosLineFmt = ExtHd3OptFmtMarker + ", " + ExtHdPosFmtMarker + "{0}";

        private Regex ExtInfRe = new Regex(@"^" + ExtInfFmtMarker + @"(?<Duration>\d+)\s*,\s*(?<Channel>[^_]+)\s*_\s*(?<Titel>.*)?\s*_\s*(?<Day>.*)\s*_\s*(?<StartTime>.*)\s*_\s*(?<EndTime>.*)\s*$", RegexOptions.CultureInvariant);
        private Regex ExtM3URe = new Regex(@"^" + ExtM3UFmtMarker, RegexOptions.CultureInvariant);
        private Regex ExtHd3Opt = new Regex(@"^" + ExtHd3OptFmtMarker + @"\s*,\s*" + ExtHdPosFmtMarker + @"(?<Position>.*)$", RegexOptions.CultureInvariant);
        private Regex ExtInf2ndRe = new Regex(@"^[^#]", RegexOptions.CultureInvariant);
        /// <summary>
        /// Item1 is for the left, Item2 for the right
        /// </summary>
        private Stack<Tuple<TvRecordsList, TvRecordsList>> UndoOperations = new Stack<Tuple<TvRecordsList, TvRecordsList>>();
        /// <summary>
        /// Item1 is for the left, Item2 for the right
        /// </summary>
        private Stack<Tuple<TvRecordsList, TvRecordsList>> RedoOperations = new Stack<Tuple<TvRecordsList, TvRecordsList>>();

        private void LeftDirBrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            BrowseDir(LeftDirTxtBox);
        }
        private void RightDirBrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            BrowseDir(RightDirTxtBox);
        }
        private static void BrowseDir(TextBox dirTextBox)
        {
            //FolderBrowserDialog dlg = new FolderBrowserDialog();
            //dlg.ShowNewFolderButton = true;
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            if (IO.Directory.Exists(dirTextBox.Text))
            {
                //dlg.SelectedPath = dirTextBox.Text;
                dialog.DefaultFileName = dirTextBox.Text;
            }
            //DialogResult res = dlg.ShowDialog();
            //if ( res == DialogResult.OK || res == DialogResult.Yes )
            //{
            //    dirTextBox.Text = dlg.SelectedPath;
            //}
            CommonFileDialogResult result = dialog.ShowDialog();
            if (result == CommonFileDialogResult.Ok)
            {
                dirTextBox.Text = dialog.FileName;
            }
        }
        private void ExitBtn_Click(object sender, EventArgs e)
        {
            if (ConfirmCancel() == Forms.DialogResult.Yes)
            {
                this.Close();
            }
        }
        private void CancelBtn_Click(object sender, EventArgs e)
        {
            ConfirmCancel();
        }

        private Forms.DialogResult ConfirmCancel()
        {
            Forms.DialogResult res = ApplyBtn.IsEnabled ?
                Forms.MessageBox.Show("Do you really want to cancel the changes? This operation cannot be undone", "Confirm Cancellation", Forms.MessageBoxButtons.YesNo, Forms.MessageBoxIcon.Question) :
                Forms.DialogResult.Yes;
            if (res == Forms.DialogResult.Yes)
            {
                if (UndoOperations.Count > 0)
                {
                    LeftRecordsLstBox.Tag = UndoOperations.Last().Item1;
                    LeftRecordsLstBox.Items.Clear();
                    foreach (TvRecord rec in LeftRecordsLstBox.Tag as TvRecordsList)
                    {
                        LeftRecordsLstBox.Items.Add(rec.VisText);
                    }
                    RightRecordsLstBox.Tag = UndoOperations.Last().Item2;
                    RightRecordsLstBox.Items.Clear();
                    foreach (TvRecord rec in RightRecordsLstBox.Tag as TvRecordsList)
                    {
                        RightRecordsLstBox.Items.Add(rec.VisText);
                    }
                }
                UndoOperations.Clear();
                RedoOperations.Clear();
                UpdateStacksLabel();
            }
            return (res);
        }
        private void ApplyBtn_Click(object sender, EventArgs e)
        {
            Forms.DialogResult res = Forms.MessageBox.Show("Do you really want to apply the changes? This operation cannot be undone", "Confirm Actions", Forms.MessageBoxButtons.YesNo, Forms.MessageBoxIcon.Question);
            if (res == Forms.DialogResult.Yes)
            {
                cts = new CancellationTokenSource();
                progress = new Progress<int>();
                ApplyHandlerAsync(cts.Token, progress);
            }
        }
        private async void ApplyHandlerAsync(CancellationToken ct, Progress<int> progress)
        {
            bool retVal = await PerformOperationsAsync(ct, progress);
            // If TaskStatus is TaskStatus.Cancelled
            if (ct.IsCancellationRequested)
            {
                Forms.MessageBox.Show("Changes scancelledby as requested", "Warning", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Warning);
            }
            else if (retVal)
            {
                Forms.MessageBox.Show("Changes sucessfully completed", "Success", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
            }
            else
            {
                Forms.MessageBox.Show("Changes could not be performed", "Failure", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Error);
            }
            UpdateStacksLabel();
            ProgressVal = "Final result: " + retVal;
            return;
        }
        private CancellationTokenSource cts = null;
        private Progress<int> progress = null;
        private async Task<bool> PerformOperationsAsync(CancellationToken ct, Progress<int> progress)
        {
            // Compute copied/moved items
            TvRecordsList copyOrMoveLeftToRight = FillCopyOrMoveList(RightRecordsLstBox.Tag as TvRecordsList, UndoOperations.Last().Item2);
            TvRecordsList copyOrMoveRightToLeft = FillCopyOrMoveList(LeftRecordsLstBox.Tag as TvRecordsList, UndoOperations.Last().Item1);
            // Compute deleted items
            TvRecordsList deleteRight = FillDeletedList(RightRecordsLstBox.Tag as TvRecordsList, UndoOperations.Last().Item2);
            TvRecordsList deleteLeft = FillDeletedList(LeftRecordsLstBox.Tag as TvRecordsList, UndoOperations.Last().Item1);
            Cursor oldCursor = this.Cursor;
            bool continueOperation = true;
            string leftRoot = LeftDirTxtBox.Text;
            string rightRoot = RightDirTxtBox.Text;
            try
            {
                this.Cursor = Cursors.Wait;
                Task[] copyTasks = new Task[Math.Max(copyOrMoveLeftToRight.Count + copyOrMoveRightToLeft.Count, 1)];
                int taskInd = 0;
                if (continueOperation)
                {

                    foreach (TvRecord rec in copyOrMoveLeftToRight)
                    {
                        copyTasks[taskInd] = FileOperations.CopyDirectoryAsync(GetRecordDir(leftRoot, rec), GetRecordDir(rightRoot, rec), null, ct);
                        taskInd++;
                        InfoLbl.Content += "Task " + taskInd + " started. ";
                        //continueOperation = FileOperations.CopyDirectory(GetRecordDir(leftRoot, rec), GetRecordDir(rightRoot, rec), null, ct);
                        //if (ct.IsCancellationRequested)
                        //{
                        //    continueOperation = false;
                        //}
                        //if (!continueOperation)
                        //{
                        //    break;
                        //}
                    }
                }
                if (continueOperation)
                {
                    foreach (TvRecord rec in copyOrMoveRightToLeft)
                    {
                        copyTasks[taskInd] = FileOperations.CopyDirectoryAsync(GetRecordDir(rightRoot, rec), GetRecordDir(leftRoot, rec), null, ct);
                        taskInd++;
                        InfoLbl.Content += "Task " + taskInd + " started. ";
                        //continueOperation = FileOperations.CopyDirectory(GetRecordDir(rightRoot, rec), GetRecordDir(leftRoot, rec), null, ct);
                        //if (ct.IsCancellationRequested)
                        //{
                        //    continueOperation = false;
                        //}
                        //if (!continueOperation)
                        //{
                        //    break;
                        //}
                    }
                }
                if (taskInd > 0)
                {
                    await Task.WhenAll(copyTasks);
                }
                if (continueOperation)
                {
                    foreach (TvRecord rec in deleteRight)
                    {
                        continueOperation = FileOperations.DeleteTree(GetRecordDir(rightRoot, rec), ct);
                        if (ct.IsCancellationRequested)
                        {
                            continueOperation = false;
                        }
                        if (!continueOperation)
                        {
                            continueOperation = false;
                            break;
                        }
                    }
                }
                if (continueOperation)
                {
                    foreach (TvRecord rec in deleteLeft)
                    {
                        continueOperation = FileOperations.DeleteTree(GetRecordDir(leftRoot, rec), ct);
                        if (ct.IsCancellationRequested)
                        {
                            continueOperation = false;
                        }
                        if (!continueOperation)
                        {
                            continueOperation = false;
                            break;
                        }
                    }
                }
                if (continueOperation)
                {
                    continueOperation = UpdateMasterFile(continueOperation, RightDirTxtBox, RightRecordsLstBox);
                }
                if (continueOperation)
                {
                    continueOperation = UpdateMasterFile(continueOperation, LeftDirTxtBox, LeftRecordsLstBox);
                }
                UndoOperations.Clear();
                RedoOperations.Clear();
            }
            finally
            {
                this.Cursor = oldCursor;
            }
            return continueOperation;
        }
        /// <summary>
        /// Get the root directory full path for a given record on the given side
        /// </summary>
        /// <param name="leftRoot"></param>
        /// <param name="rec"></param>
        /// <returns></returns>
        private static string GetRecordDir(string leftRoot, TvRecord rec)
        {
            string retVal = IO.Path.GetDirectoryName(IO.Path.Combine(leftRoot, FileOperations.GetRelativePath(rec.Location.Replace('/', IO.Path.DirectorySeparatorChar))));
            return retVal;
        }
        private void UpdateTransferButtons()
        {
            CopyToRightBtn.IsEnabled = LeftRecordsLstBox.SelectedItems.Count > 0;
            MoveToRightBtn.IsEnabled = LeftRecordsLstBox.SelectedItems.Count > 0;
            CopyToLeftBtn.IsEnabled = RightRecordsLstBox.SelectedItems.Count > 0;
            MoveToLeftBtn.IsEnabled = RightRecordsLstBox.SelectedItems.Count > 0;
        }
        private void FillRecordsList(TextBox textBox, ListBox listBox)
        {
            if (textBox != null && listBox != null && listBox.Tag != null)
            {
                (listBox.Tag as TvRecordsList).Clear();
                listBox.Items.Clear();
                (listBox.Tag as TvRecordsList).Clear();
                string dir = textBox.Text.Trim();
                UndoOperations.Clear();
                RedoOperations.Clear();
                if (IO.Directory.Exists(dir))
                {
                    string masterFile = IO.Path.Combine(dir, MasterFile);
                    if (IO.File.Exists(masterFile))
                    {
                        using (
                            IO.StreamReader master = new IO.StreamReader(masterFile, Encoding.GetEncoding("iso8859-1")))
                        {
                            string nextLine = null;
                            string currLine = null;
                            bool firstLine = true;
                            while (!master.EndOfStream)
                            {
                                if (nextLine == null)
                                {
                                    currLine = master.ReadLine();
                                }
                                else
                                {
                                    currLine = nextLine;
                                    nextLine = null;
                                }
                                if (!String.IsNullOrEmpty(currLine))
                                {
                                    if (!String.IsNullOrEmpty(currLine) && firstLine)
                                    {
                                        if (!ExtM3URe.IsMatch(currLine))
                                        {
                                            MessageBox.Show("Not Extended M3U format, not Supported");
                                            break;
                                        }
                                        firstLine = false;
                                    }
                                    else
                                    {
                                        Match m = ExtInfRe.Match(currLine);
                                        if (m.Success)
                                        {
                                            if (!master.EndOfStream)
                                            {
                                                nextLine = master.ReadLine();
                                                if (ExtInf2ndRe.IsMatch(nextLine))
                                                {
                                                    int duration = int.Parse(m.Groups["Duration"].Value.Trim());

                                                    TvRecord newRec = new TvRecord(
                                                        duration,
                                                        m.Groups["Channel"].Value.Trim(),
                                                        m.Groups["Titel"].Value.Trim(),
                                                        m.Groups["Day"].Value.Trim(),
                                                        m.Groups["StartTime"].Value.Trim(),
                                                        m.Groups["EndTime"].Value.Trim(),
                                                        nextLine);
                                                    int ind = listBox.Items.Add(newRec.VisText);
                                                    nextLine = null;
                                                    if (!master.EndOfStream)
                                                    {
                                                        nextLine = master.ReadLine();
                                                        m = ExtHd3Opt.Match(nextLine);
                                                        if (m.Success)
                                                        {
                                                            newRec.Position = m.Groups["Position"].Value.Trim();
                                                            nextLine = null;
                                                        }
                                                    }
                                                    (listBox.Tag as TvRecordsList).Add(newRec);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                UpdateStacksLabel();
            }
        }


        private void RescanBtn_Click(object sender, EventArgs e)
        {
            FillRecordsList(LeftDirTxtBox, LeftRecordsLstBox);
            FillRecordsList(RightDirTxtBox, RightRecordsLstBox);
        }
        private void RefreshListboxes()
        {
            LeftRecordsLstBox.Items.Clear();
            foreach (TvRecord rec in LeftRecordsLstBox.Tag as TvRecordsList)
            {
                LeftRecordsLstBox.Items.Add(rec.VisText);
            }
            RightRecordsLstBox.Items.Clear();
            foreach (TvRecord rec in RightRecordsLstBox.Tag as TvRecordsList)
            {
                RightRecordsLstBox.Items.Add(rec.VisText);
            }
        }

        private void RefreshBtn_Click(object sender, EventArgs e)
        {
            RefreshListboxes();
        }

        private bool UpdateMasterFile(bool continueOperation, TextBox textBox, ListBox listBox)
        {
            string dir = textBox.Text.Trim();
            string masterFile = IO.Path.Combine(dir, MasterFile);
            string masterFile_1 = masterFile + ".1";
            string masterFile_2 = masterFile + ".2";
            if (continueOperation)
            {
                try
                {
                    if (IO.File.Exists(masterFile_2))
                    {
                        IO.File.Delete(masterFile_2);
                    }
                }
                catch
                {
                    // swallow
                }
            }
            if (continueOperation)
            {
                try
                {
                    IO.File.Move(masterFile_1, masterFile_2);
                }
                catch (IO.FileNotFoundException)
                {
                    // swallow
                }
                catch
                {
                    continueOperation = false;
                }
            }
            if (continueOperation)
            {
                try
                {
                    IO.File.Move(masterFile, masterFile_1);
                }
                catch (IO.FileNotFoundException)
                {
                    // swallow
                }
                catch
                {
                    continueOperation = false;
                }
            }
            if (continueOperation)
            {
                try
                {
                    using (IO.FileStream masterStream = new IO.FileStream(masterFile, IO.FileMode.Create))
                    {
                        using (IO.StreamWriter master = new IO.StreamWriter(masterStream, Encoding.GetEncoding("iso8859-1")))
                        {
                            master.WriteLine(ExtM3UFmtMarker);
                            foreach (TvRecord rec in listBox.Tag as TvRecordsList)
                            {
                                master.WriteLine(String.Format(ExtM3ULineFmt, rec.Duration, rec.Channel, rec.Titel, rec.Day, rec.StartTime, rec.EndTime));
                                master.WriteLine(rec.Location);
                                if (!String.IsNullOrWhiteSpace(rec.Position))
                                {
                                    master.WriteLine(String.Format(ExtM3UPosLineFmt, rec.Position));
                                }
                            }
                        }
                    }
                    if (!IO.File.Exists(masterFile_1))
                    {
                        IO.File.Copy(masterFile, masterFile_1);
                    }
                    if (!IO.File.Exists(masterFile_2))
                    {
                        IO.File.Copy(masterFile_1, masterFile_2);
                    }
                }
                catch
                {
                    continueOperation = false;
                }
            }
            return continueOperation;
        }
        private static TvRecordsList FillDeletedList(TvRecordsList newList, TvRecordsList oldList)
        {
            TvRecordsList retVal = new TvRecordsList();
            foreach (TvRecord oldRec in oldList)
            {
                bool delete = true;
                foreach (TvRecord newRec in newList)
                {
                    if (oldRec.VisText == newRec.VisText)
                    {
                        delete = false;
                        break;
                    }
                }
                if (delete)
                {
                    retVal.Add(oldRec);
                }
            }
            return (retVal);
        }
        private const string UndoFmt = "UndoStack: {0} ({2}), RedoStack: {1} ({3})";
        private const string UndoMbrFmt = "{{{0},{1}}}";
        private void UpdateStacksLabel()
        {
            StringBuilder strBLeft = new StringBuilder();
            foreach (Tuple<TvRecordsList, TvRecordsList> t in UndoOperations)
            {
                strBLeft.Append(String.Format(UndoMbrFmt, t.Item1.Count, t.Item2.Count));
            }
            StringBuilder strBRight = new StringBuilder();
            foreach (Tuple<TvRecordsList, TvRecordsList> t in RedoOperations)
            {
                strBRight.Append(String.Format(UndoMbrFmt, t.Item1.Count, t.Item2.Count));
            }
            UndoStackLbl.Content = String.Format(UndoFmt, UndoOperations.Count, RedoOperations.Count,
                strBLeft.ToString(), strBRight.ToString());

            StringBuilder strB = new StringBuilder("Tags: Left: ");
            strB.Append((LeftRecordsLstBox.Tag as TvRecordsList).Count);
            strB.Append(", Right: ");
            strB.Append((RightRecordsLstBox.Tag as TvRecordsList).Count);
            ListBoxContentsLbl.Content = strB.ToString();


            ApplyBtn.IsEnabled = UndoOperations.Count > 0;
            // ExitBtn.IsEnabled = UndoOperations.Count > 0;
            UpdateTransferButtons();
        }
        private void DefCopy(List<string> handledItems, ListBox srcLstBox, ListBox destLstBox, OperationType opType, Direction dir)
        {
            TvRecordsList dest = destLstBox.Tag as TvRecordsList;
            TvRecordsList src = srcLstBox.Tag as TvRecordsList;
            if (dest != null && src != null)
            {
                bool reallyChanged = false;
                Tuple<TvRecordsList, TvRecordsList> oldState = dir == Direction.ToLeft ?
                    new Tuple<TvRecordsList, TvRecordsList>(new TvRecordsList(destLstBox.Tag as TvRecordsList), new TvRecordsList(srcLstBox.Tag as TvRecordsList)) :
                    new Tuple<TvRecordsList, TvRecordsList>(new TvRecordsList(srcLstBox.Tag as TvRecordsList), new TvRecordsList(destLstBox.Tag as TvRecordsList));
                List<int> deletedIndices = new List<int>();
                foreach (string recVisText in handledItems)
                {
                    if (!destLstBox.Items.Contains(recVisText) || opType == OperationType.Move)
                    {
                        foreach (TvRecord rec in src)
                        {
                            if (rec.VisText == recVisText)
                            {
                                if (!destLstBox.Items.Contains(recVisText))
                                {
                                    dest.Add(rec);
                                    destLstBox.Items.Add(rec.VisText);
                                    reallyChanged = true;
                                }
                                if (opType == OperationType.Move)
                                {
                                    deletedIndices.Add(src.IndexOf(rec));
                                }
                                break;
                            }
                        }
                    }
                }
                foreach (int ind in from l in deletedIndices orderby l descending select l)
                {
                    src.RemoveAt(ind);
                    srcLstBox.Items.RemoveAt(ind);
                    reallyChanged = true;
                }
                if (reallyChanged)
                {
                    PushOperation(oldState);
                }
            }
        }
        private void PushOperation(Tuple<TvRecordsList, TvRecordsList> oldState)
        {
            UndoOperations.Push(oldState);
            UpdateStacksLabel();
        }
        private void CopyToRightBtn_Click(object sender, EventArgs e)
        {
            List<string> selectionList = GetSelectedItems(LeftRecordsLstBox);
            DefCopy(selectionList, LeftRecordsLstBox, RightRecordsLstBox, OperationType.Copy, Direction.ToRight);
        }

        private static List<string> GetSelectedItems(ListBox lstBox)
        {
            List<string> selectionList = new List<string>(lstBox.SelectedItems.Count);
            foreach (string rec in lstBox.SelectedItems)
            {
                selectionList.Add(rec);
            }
            return selectionList;
        }

        private void CopyToLeftBtn_Click(object sender, EventArgs e)
        {
            List<string> selectionList = GetSelectedItems(RightRecordsLstBox);
            DefCopy(selectionList, RightRecordsLstBox, LeftRecordsLstBox, OperationType.Copy, Direction.ToLeft);
        }

        private void MoveToLeftBtn_Click(object sender, EventArgs e)
        {
            List<string> selectionList = GetSelectedItems(RightRecordsLstBox);
            DefCopy(selectionList, RightRecordsLstBox, LeftRecordsLstBox, OperationType.Move, Direction.ToLeft);
        }

        private void MoveToRightBtn_Click(object sender, EventArgs e)
        {
            List<string> selectionList = GetSelectedItems(LeftRecordsLstBox);
            DefCopy(selectionList, LeftRecordsLstBox, RightRecordsLstBox, OperationType.Move, Direction.ToRight);
        }
        private enum OperationType
        {
            Copy = 0, // Default because safer
            Move
        }
        private enum Direction
        {
            ToLeft,
            ToRight
        }
        private void LeftDirTxtBox_TextChanged(object sender, EventArgs e)
        {
            FillRecordsList(sender as TextBox, LeftRecordsLstBox);
        }
        private void RightDirTxtBox_TextChanged(object sender, EventArgs e)
        {
            FillRecordsList(sender as TextBox, RightRecordsLstBox);
        }

        private void RightRecordsLstBox_KeyUp(object sender, KeyEventArgs e)
        {
            ListBox listBox = sender as ListBox;
            HandleKeyInListBox(e, listBox);
        }

        private void LeftRecordsLstBox_KeyUp(object sender, KeyEventArgs e)
        {
            ListBox listBox = sender as ListBox;
            HandleKeyInListBox(e, listBox);
        }
        private void LeftRecordsLstBox_SelectedValueChanged(object sender, EventArgs e)
        {
            UpdateTransferButtons();
        }

        private void RightRecordsLstBox_SelectedValueChanged(object sender, EventArgs e)
        {
            UpdateTransferButtons();
        }

        private void HandleKeyInListBox(KeyEventArgs e, ListBox listBox)
        {
            if (listBox != null)
            {
                switch (e.Key)
                {
                    case Key.Delete:
                        bool reallyChanged = false;
                        Tuple<TvRecordsList, TvRecordsList> oldState = new Tuple<TvRecordsList, TvRecordsList>(new TvRecordsList(LeftRecordsLstBox.Tag as TvRecordsList), new TvRecordsList(RightRecordsLstBox.Tag as TvRecordsList));
                        List<int> selectedInd = new List<int>();
                        foreach (string recStr in listBox.SelectedItems)
                        {
                            selectedInd.Add(listBox.SelectedItems.IndexOf(recStr));
                        }
                        foreach (int ind in from s in selectedInd orderby s descending select s)
                        {
                            listBox.Items.RemoveAt(ind);
                            (listBox.Tag as TvRecordsList).RemoveAt(ind);
                            reallyChanged = true;
                        }
                        if (reallyChanged)
                        {
                            PushOperation(oldState);
                        }
                        break;
                    case Key.Z:
                        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                        {
                            if (UndoOperations.Count > 0)
                            {
                                Tuple<TvRecordsList, TvRecordsList> prevState = UndoOperations.Pop();
                                RedoOperations.Push(new Tuple<TvRecordsList, TvRecordsList>(new TvRecordsList(LeftRecordsLstBox.Tag as TvRecordsList), new TvRecordsList(RightRecordsLstBox.Tag as TvRecordsList)));
                                LeftRecordsLstBox.Tag = prevState.Item1;
                                RightRecordsLstBox.Tag = prevState.Item2;
                                LeftRecordsLstBox.Items.Clear();
                                foreach (TvRecord rec in prevState.Item1)
                                {
                                    LeftRecordsLstBox.Items.Add(rec.VisText);
                                }
                                RightRecordsLstBox.Items.Clear();
                                foreach (TvRecord rec in prevState.Item2)
                                {
                                    RightRecordsLstBox.Items.Add(rec.VisText);
                                }
                                UpdateStacksLabel();
                            }
                        }
                        break;
                    case Key.Y:
                        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                        {
                            if (RedoOperations.Count > 0)
                            {
                                Tuple<TvRecordsList, TvRecordsList> nextState = RedoOperations.Pop();
                                UndoOperations.Push(new Tuple<TvRecordsList, TvRecordsList>(new TvRecordsList(LeftRecordsLstBox.Tag as TvRecordsList), new TvRecordsList(RightRecordsLstBox.Tag as TvRecordsList)));
                                LeftRecordsLstBox.Tag = nextState.Item1;
                                RightRecordsLstBox.Tag = nextState.Item2;
                                LeftRecordsLstBox.Items.Clear();
                                foreach (TvRecord rec in nextState.Item1)
                                {
                                    LeftRecordsLstBox.Items.Add(rec.VisText);
                                }
                                RightRecordsLstBox.Items.Clear();
                                foreach (TvRecord rec in nextState.Item2)
                                {
                                    RightRecordsLstBox.Items.Add(rec.VisText);
                                }
                                UpdateStacksLabel();
                                RefreshListboxes();
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
        }


        private static TvRecordsList FillCopyOrMoveList(TvRecordsList newList, TvRecordsList oldList)
        {
            TvRecordsList retVal = new TvRecordsList();
            foreach (TvRecord newRec in newList)
            {
                bool copy = true;
                foreach (TvRecord oldRec in oldList)
                {
                    if (oldRec.VisText == newRec.VisText)
                    {
                        copy = false;
                        break;
                    }
                }
                if (copy)
                {
                    retVal.Add(newRec);
                }
            }
            return (retVal);
        }
        private ListBox dragSource = null;
        private void LeftRecordsLstBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

            RecordsLstBox_PrepareDrag(sender, e);
        }
        private void RightRecordsLstBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

            RecordsLstBox_PrepareDrag(sender, e);
        }

        private void RecordsLstBox_PrepareDrag(object sender, MouseButtonEventArgs e)
        {
            ListBox lstBox = sender as ListBox;
            object data = null;
            if (lstBox != null)
            {
                dragSource = lstBox;
                // Toggle selection status
                string ret = GetDataFromListBox(dragSource, e.GetPosition(dragSource)) as string;
                if ( dragSource.SelectedItems.Contains(ret))
                {
                    dragSource.SelectedItems.Remove(ret);
                }
                else
                {
                    dragSource.SelectedItems.Add(ret);
                }
                int ind = dragSource.Items.IndexOf(ret);

                if (lstBox.SelectedItems.Count > 0)
                {
                    data = GetSelectedItems(lstBox);
                }
                else
                {
                    // string ret = GetDataFromListBox(dragSource, e.GetPosition(dragSource)) as string;
                    if (ret != null)
                    {
                        data = new List<string>(1) { ret };
                    }
                }
                if (data != null)
                {
                    DragDrop.DoDragDrop(dragSource, data, DragDropEffects.Move);
                }
            }
        }

        private static object GetDataFromListBox(ListBox source, Point point)
        {
            object retVal = null;
            UIElement element = source.InputHitTest(point) as UIElement;
            if (element != null)
            {
                retVal = DependencyProperty.UnsetValue;
                while (retVal == DependencyProperty.UnsetValue)
                {
                    retVal = source.ItemContainerGenerator.ItemFromContainer(element) as string;

                    if (retVal == DependencyProperty.UnsetValue)
                    {
                        element = VisualTreeHelper.GetParent(element) as UIElement;
                    }

                    if (element == source)
                    {
                        retVal = null;
                    }
                }
                //if (retVal != DependencyProperty.UnsetValue)
                //{
                //    return retVal;
                //}
            }
            return retVal;
        }
        private void RecordsLstBox_Drop(object sender, DragEventArgs e)
        {
            ListBox parent = sender as ListBox;
            if (parent != null && dragSource != null)
            {
                Direction dir = parent == RightRecordsLstBox ? Direction.ToRight : Direction.ToLeft;
                if (parent == dragSource)
                {  // Only reorganize within the same box, DOES NOT WORK YET
                    List<string> data = e.Data.GetData(typeof(List<string>)) as List<string>;
                    string ret = GetDataFromListBox(parent, e.GetPosition(parent)) as string;
                    if (ret != null && data != null)
                    {
                        Tuple<TvRecordsList, TvRecordsList> oldState = new Tuple<TvRecordsList, TvRecordsList>(new TvRecordsList(LeftRecordsLstBox.Tag as TvRecordsList), new TvRecordsList(RightRecordsLstBox.Tag as TvRecordsList));
                        int ind = parent.Items.IndexOf(ret);
                        foreach (string movedRec in data)
                        {
                            int movedInd = parent.Items.IndexOf(movedRec);
                            if (movedInd < ind)
                            {
                                parent.Items.Insert(ind + 1, movedRec);
                                (parent.Tag as List<TvRecord>).Insert(ind + 1, (parent.Tag as List<TvRecord>)[movedInd]);
                                parent.Items.RemoveAt(movedInd);
                                (parent.Tag as List<TvRecord>).RemoveAt(movedInd);
                            }
                            else if (movedInd > ind)
                            {
                                parent.Items.Insert(ind + 1, movedRec);
                                (parent.Tag as List<TvRecord>).Insert(ind + 1, (parent.Tag as List<TvRecord>)[movedInd]);
                                parent.Items.RemoveAt(movedInd + 1);
                                (parent.Tag as List<TvRecord>).RemoveAt(movedInd + 1);

                            }
                        }
                        PushOperation(oldState);
                    }
                }
                else
                {
                    List<string> data = e.Data.GetData(typeof(List<string>)) as List<string>;
                    if (data != null)
                    {
                        DefCopy(data, dragSource, parent, OperationType.Move, dir);
                    }
                }
            }
        }
    }
}
