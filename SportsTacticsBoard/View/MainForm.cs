using TacticsBoard.PlayingSurfaceTypes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace TacticsBoard
{
    public partial class MainForm : Form, ICustomLabelProvider
    {
        private readonly SavedLayoutManager commonSavedLayoutManager;
        private readonly SavedLayoutManager userSavedLayoutManager;
        private LayoutSequence currentSequence;
        private int positionInSequence;
        private string fileName = "";
        private readonly string fileFilter = Properties.Resources.ResourceManager.GetString("FileFilter");
        private readonly string saveAsImageFileFilter = Properties.Resources.ResourceManager.GetString("SaveAsImageFileFilter");
        private readonly string saveAsAVIFileFilter = Properties.Resources.ResourceManager.GetString("SaveAsAViFileFilter");
        private readonly string originalCaption;
        private bool sequenceDirty;

        private bool IsDocumentDirty
        {
            get
            {
                return sequenceDirty || fieldControl.IsViewDirty || fieldControl.IsDirty;
            }
        }

        public MainForm()
        {
            commonSavedLayoutManager = new SavedLayoutManager(SavedLayoutManager.CommonLayoutPath);
            userSavedLayoutManager = new SavedLayoutManager(SavedLayoutManager.UserLayoutPath);
            InitializeComponent();
            StopPlayingSequence();

            if (!FileNew(true, false))
            {
                System.Environment.Exit(0);
            }
            originalCaption = Text;
            fieldControl.CustomLabelProvider = this;
            fieldControl.IsDirtyChanged += new EventHandler(FieldControl_IsDirtyChanged);
            fieldControl.IsViewDirtyChanged += new EventHandler(FieldControl_IsViewDirtyChanged);
            fieldControl.KeyDown += new KeyEventHandler(fieldControl_KeyDown);
        }

        void FieldControl_IsViewDirtyChanged(object sender, EventArgs e)
        {
            UpdateFileMenuItems();
        }

        public void RecordNew()
        {
            positionInSequence = RecordPositionToSequence(false, positionInSequence + 1, currentSequence);
            fieldControl.SetNextLayout(GetNextLayout());
            UpdateSequenceControls();
        }

        private void RecordNewPositionButton_Click(object sender, EventArgs e)
        {
            if (null != currentSequence) {
                positionInSequence = RecordPositionToSequence(false, positionInSequence + 1, currentSequence);
                fieldControl.SetNextLayout(GetNextLayout());
                UpdateSequenceControls();
            }
        }

        private FieldLayout GetNextLayout()
        {
            return currentSequence.GetLayout(positionInSequence + 1);
        }

        private void FieldControl_IsDirtyChanged(object sender, EventArgs e)
        {
            UpdateSequenceControls();
            UpdateFileMenuItems();
        }

        private void CommonSavedLayoutMenuItem_Click(object sender, EventArgs e)
        {
            StopPlayingSequence();
            ToolStripMenuItem mi = (ToolStripMenuItem)sender;
            FieldLayout layout = commonSavedLayoutManager.GetLayoutForMenuItem(mi, SafeGetCurrentFieldTypeTag());
            fieldControl.SetLayout(layout);
        }

        private void UserSavedLayoutMenuItem_Click(object sender, EventArgs e)
        {
            StopPlayingSequence();
            ToolStripMenuItem mi = (ToolStripMenuItem)sender;
            FieldLayout layout = userSavedLayoutManager.GetLayoutForMenuItem(mi, SafeGetCurrentFieldTypeTag());
            fieldControl.SetLayout(layout);
        }

        private void UpdateFileMenuItems()
        {
            saveSequenceAsMenuItem.Enabled = (fieldControl.FieldType != null);
            saveSequenceMenuItem.Enabled = (fieldControl.FieldType != null) && (IsDocumentDirty || string.IsNullOrEmpty(fileName));
        }


        private string SafeGetCurrentFieldTypeTag()
        {
            if (fieldControl.FieldType == null) {
                return null;
            }
            return fieldControl.FieldType.Tag;
        }

        private void UpdateSequenceControls()
        {
            if (null != currentSequence) {
                if (currentSequence.NumberOfLayouts == 0) {
                    currentLayoutNumber.Text =
                      Properties.Resources.ResourceManager.GetString("CurrentLayoutNumber_Empty");
                } else {
                    string formatString =
                      Properties.Resources.ResourceManager.GetString("CurrentLayoutNumber_Format");
                    currentLayoutNumber.Text =
                      string.Format(CultureInfo.CurrentUICulture, formatString, positionInSequence + 1, currentSequence.NumberOfLayouts);
                }
                goToFirstToolStripButton.Enabled = (positionInSequence > 0);
                previousLayoutInSequence.Enabled = (positionInSequence > 0);
                nextLayoutInSequence.Enabled = ((positionInSequence + 1) < currentSequence.NumberOfLayouts);
                goToLastToolStripButton.Enabled = ((positionInSequence + 1) < currentSequence.NumberOfLayouts);
                revertCurrentLayoutToSavedButton.Enabled = (!IsPlayingSequence) && (currentSequence.NumberOfLayouts > 0) && (fieldControl.IsDirty);
                removeCurrentPositionFromSequenceButton.Enabled = (!IsPlayingSequence) && (currentSequence.NumberOfLayouts > 1);
                showMovementButton.Checked = fieldControl.ShowMovementLines;
                showMovementButton.Enabled = true;
                recordOverCurrentPositionButton.Enabled = !IsPlayingSequence;
                recordNewPositionButton.Enabled = !IsPlayingSequence;
                playToolStripButton.Enabled = currentSequence.NumberOfLayouts > 1;
            } else {
                previousLayoutInSequence.Enabled = false;
                nextLayoutInSequence.Enabled = false;
                revertCurrentLayoutToSavedButton.Enabled = false;
                removeCurrentPositionFromSequenceButton.Enabled = false;
                showMovementButton.Checked = false;
                showMovementButton.Enabled = false;
                recordOverCurrentPositionButton.Enabled = false;
                recordNewPositionButton.Enabled = false;
                goToFirstToolStripButton.Enabled = false;
                goToLastToolStripButton.Enabled = false;
                playToolStripButton.Enabled = false;
            }
            if (IsPlayingSequence) {
                playToolStripButton.Image = Properties.Resources.PauseHS;
            } else {
                playToolStripButton.Image = Properties.Resources.PlayHS;
            }
        }

        private void PreviousLayoutInSequence_Click(object sender, EventArgs e)
        {
            StopPlayingSequence();
            MoveToPreviousLayout();
        }

        private void MoveToPreviousLayout()
        {
            if (null == currentSequence) {
                return;
            }
            if (positionInSequence > 0) {
                if ((currentSequence.NumberOfLayouts > 0) && (fieldControl.IsDirty)) {
                    DialogResult dr = GlobalizationAwareMessageBox.Show(
                      this,
                      Properties.Resources.ResourceManager.GetString("SaveSequenceEntryBeforeSwitchingEntries"),
                      this.Text,
                      MessageBoxButtons.YesNoCancel,
                      MessageBoxIcon.Question,
                      MessageBoxDefaultButton.Button1,
                      (MessageBoxOptions)0);
                    switch (dr) {
                        case DialogResult.Yes:
                            RecordPositionToSequence(true, positionInSequence, currentSequence);
                            break;
                        case DialogResult.Cancel:
                            return;
                    }
                }
                positionInSequence--;
                RestorePositionFromSequence(positionInSequence, currentSequence, GetNextLayout());
                UpdateSequenceControls();
            }
        }

        private void NextLayoutInSequence_Click(object sender, EventArgs e)
        {
            StopPlayingSequence();
            MoveToNextLayout();
        }

        private void MoveToNextLayout()
        {
            if (null == currentSequence) {
                return;
            }
            if (positionInSequence + 1 < currentSequence.NumberOfLayouts) {
                if ((currentSequence.NumberOfLayouts > 0) && (fieldControl.IsDirty)) {
                    DialogResult dr =
                      GlobalizationAwareMessageBox.Show(this,
                                              Properties.Resources.ResourceManager.GetString("SaveSequenceEntryBeforeSwitchingEntries"),
                                              this.Text,
                                              MessageBoxButtons.YesNoCancel,
                                              MessageBoxIcon.Question,
                                              MessageBoxDefaultButton.Button1,
                                              (MessageBoxOptions)0);
                    switch (dr) {
                        case DialogResult.Yes:
                            RecordPositionToSequence(true, positionInSequence, currentSequence);
                            break;
                        case DialogResult.Cancel:
                            return;
                    }
                }
                positionInSequence++;
                RestorePositionFromSequence(positionInSequence, currentSequence, GetNextLayout());
                UpdateSequenceControls();
            }
        }

        private void RecordOverCurrentPositionButton_Click(object sender, EventArgs e)
        {
            if (null != currentSequence) {
                RecordPositionToSequence(true, positionInSequence, currentSequence);
                UpdateSequenceControls();
            }
        }

        private void RemoveCurrentPositionFromSequenceButton_Click(object sender, EventArgs e)
        {
            if ((null != currentSequence) && (currentSequence.NumberOfLayouts > 0)) {
                currentSequence.RemoveFromSequence(positionInSequence);
                if (positionInSequence >= currentSequence.NumberOfLayouts) {
                    positionInSequence--;
                }
                fieldControl.SetNextLayout(GetNextLayout());
                UpdateSequenceControls();
            }
        }

        private void RevertCurrentLayoutToSavedButton_Click(object sender, EventArgs e)
        {
            if (null != currentSequence) {
                RestorePositionFromSequence(positionInSequence, currentSequence, GetNextLayout());
                UpdateSequenceControls();
            }
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StopPlayingSequence();
            Application.Exit();
        }

        private void UpdateCaption()
        {
            string title = "Canoepolo Tactics Board";
            if (!string.IsNullOrEmpty(fileName)) {
                title = string.Format(
                  CultureInfo.CurrentCulture,
                  Properties.Resources.TitleFormatString,
                  originalCaption,
                  System.IO.Path.GetFileName(fileName));
            }
            Text = title;
        }

        private void OpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StopPlayingSequence();

            using (OpenFileDialog openFileDialog = new OpenFileDialog()) {
                openFileDialog.CheckFileExists = true;
                openFileDialog.Filter = fileFilter;
                openFileDialog.RestoreDirectory = true;
                openFileDialog.SupportMultiDottedExtensions = true;
                openFileDialog.Multiselect = false;
                openFileDialog.InitialDirectory = GetDefaultSequenceSaveLocation(true);

                DialogResult res = openFileDialog.ShowDialog();
                if (res == DialogResult.OK) {
                    try {
                        XmlSerializer serializer = new XmlSerializer(typeof(LayoutSequence));
                        using (FileStream fs = new FileStream(openFileDialog.FileName, FileMode.Open)) {
                            LayoutSequence seq = (LayoutSequence)serializer.Deserialize(fs);
                            if (seq != null) {
                                // Locate the field type interface for the field specified by
                                // this saved file
                                IPlayingSurfaceType newFieldType = FindFieldType(seq.FieldTypeTag);
                                newFieldType.amountOfBalls = seq.AmountOfBalls;
                                newFieldType.PlayersPerTeam = seq.PlayersPerTeam;
                                if (newFieldType == null) {
                                    string msgFormat = Properties.Resources.ResourceManager.GetString("FailedToOpenFileFormatStr");
                                    GlobalizationAwareMessageBox.Show(
                                      this,
                                      String.Format(CultureInfo.CurrentUICulture, msgFormat, openFileDialog.FileName),
                                      this.Text,
                                      MessageBoxButtons.OK,
                                      MessageBoxIcon.Error,
                                      MessageBoxDefaultButton.Button1,
                                      (MessageBoxOptions)0);
                                } else {
                                    fieldControl.FieldType = newFieldType;
                                    fieldControl.SetNextLayout(null);
                                    fieldControl.IsDirty = false;
                                    fieldControl.IsViewDirty = false;
                                    positionInSequence = 0;
                                    currentSequence = seq;
                                    sequenceDirty = false;
                                    RestorePositionFromSequence(positionInSequence, currentSequence, GetNextLayout());
                                    fileName = openFileDialog.FileName;
                                    UpdateCaption();
                                    UpdateFileMenuItems();

                                    UpdateSequenceControls();
                                    UpdateViewMenuItems();
                                }
                            } else {
                                string msgFormat = Properties.Resources.ResourceManager.GetString("FailedToOpenFileFormatStr");
                                GlobalizationAwareMessageBox.Show(
                                  this,
                                  String.Format(CultureInfo.CurrentUICulture, msgFormat, openFileDialog.FileName),
                                  this.Text,
                                  MessageBoxButtons.OK,
                                  MessageBoxIcon.Error,
                                  MessageBoxDefaultButton.Button1,
                                  (MessageBoxOptions)0);
                            }
                        }
                    } catch (System.IO.IOException /*exception*/) {
                        string msgFormat = Properties.Resources.ResourceManager.GetString("FailedToOpenFileFormatStr");
                        GlobalizationAwareMessageBox.Show(
                          this,
                          String.Format(CultureInfo.CurrentUICulture, msgFormat, openFileDialog.FileName),
                          this.Text,
                          MessageBoxButtons.OK,
                          MessageBoxIcon.Error,
                          MessageBoxDefaultButton.Button1,
                          (MessageBoxOptions)0);
                    }
                }
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(fileName)) {
                FileSaveAs();
            } else {
                FileSave();
            }
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FileSaveAs();
        }

        private void FileSave()
        {
            StopPlayingSequence();

            if (null != currentSequence) {
                currentSequence.PlayersPerTeam = fieldControl.FieldType.PlayersPerTeam;
                currentSequence.AmountOfBalls = fieldControl.FieldType.amountOfBalls;
                XmlSerializer serializer = new XmlSerializer(typeof(LayoutSequence));
                using (TextWriter writer = new StreamWriter(fileName)) {
                    serializer.Serialize(writer, currentSequence);
                }
            }
            sequenceDirty = false;
            fieldControl.IsViewDirty = false;
            UpdateFileMenuItems();
        }

        private static string GetDefaultSequenceSaveLocation(bool attemptToCreate)
        {
            string fn1 = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            fn1 = Path.Combine(fn1, " Tactics Board");
            fn1 = Path.Combine(fn1, "Sequences");
            if (attemptToCreate) {
                // Ensure we have a folder to save files in
                if (!System.IO.Directory.Exists(fn1)) {
                    try {
                        System.IO.Directory.CreateDirectory(fn1);
                    } catch (System.IO.IOException) {
                    }
                }
            }
            return fn1;
        }

        private void FileSaveAs()
        {
            StopPlayingSequence();

            using (SaveFileDialog saveFileDialog = new SaveFileDialog()) {
                saveFileDialog.Filter = fileFilter;
                saveFileDialog.RestoreDirectory = true;
                saveFileDialog.SupportMultiDottedExtensions = true;
                saveFileDialog.InitialDirectory = GetDefaultSequenceSaveLocation(true);

                DialogResult res = saveFileDialog.ShowDialog();
                if (res == DialogResult.OK) {
                    fileName = saveFileDialog.FileName;
                    UpdateCaption();
                    FileSave();
                }
            }
        }

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StopPlayingSequence();

            FileNew(true, false);
        }

        static private List<IPlayingSurfaceType> FindPlayingSurfacesInAssembly(Assembly a)
        {
            List<IPlayingSurfaceType> surfaceTypes = new List<IPlayingSurfaceType>();

            Type[] types = a.GetTypes();
            for (int i = 0; i < types.Length; i++) {
                if ((types[i].IsClass) && (!types[i].IsAbstract)) {
                    Type iface = types[i].GetInterface(typeof(IPlayingSurfaceType).Name);
                    if (iface != null) {
                        IPlayingSurfaceType surfaceType = Activator.CreateInstance(types[i]) as IPlayingSurfaceType;
                        surfaceTypes.Add(surfaceType);
                    }
                }
            }
            return surfaceTypes;
        }

        internal static List<IPlayingSurfaceType> AvailableFieldTypes
        {
            get
            {
                return FindPlayingSurfacesInAssembly(Assembly.GetExecutingAssembly());
            }
        }

        private static IPlayingSurfaceType FindFieldType(string tag)
        {
            List<IPlayingSurfaceType> fieldTypes = AvailableFieldTypes;
            foreach (IPlayingSurfaceType ft in fieldTypes) {
                if (ft.Tag == tag) {
                    return ft;
                }
            }
            return null;
        }

        private static IPlayingSurfaceType LoadDefaultFieldType()
        {
            string defaultFieldType = global::TacticsBoard.Properties.Settings.Default.DefaultFieldType;
            if (defaultFieldType.Length == 0) {
                return null;
            };
            List<IPlayingSurfaceType> fieldTypes = AvailableFieldTypes;
            foreach (IPlayingSurfaceType ft in fieldTypes) {
                if (ft.Name == defaultFieldType) {
                    return ft;
                }
            }
            return null;
        }

        private bool FileNew(bool saveAsDefaultChecked, bool alwaysAskForFieldType)
        {
            StopPlayingSequence();

            IPlayingSurfaceType newFieldType = fieldControl.FieldType;
            if (newFieldType == null)
            {
                newFieldType = LoadDefaultFieldType();
            }
            if ((newFieldType == null) || (alwaysAskForFieldType))
            {
                newFieldType = SelectPlayingSurfaceType.AskUserForFieldType();
                if (null == newFieldType)
                {
                    return false;
                }

            }

            fileName = "";
            fieldControl.FieldType = newFieldType;
            fieldControl.SetNextLayout(null);
            fieldControl.IsDirty = false;
            fieldControl.IsViewDirty = false;
            positionInSequence = 0;
            sequenceDirty = false;
            currentSequence = new LayoutSequence(fieldControl.FieldType.Tag);
            RecordPositionToSequence(false, positionInSequence, currentSequence);
            UpdateCaption();
            UpdateFileMenuItems();

            UpdateViewMenuItems();
            UpdateSequenceControls();
            return true;
        }

        private void showMovementButton_Click(object sender, EventArgs e)
        {
            ToolStripButton b = (ToolStripButton)sender;
            fieldControl.ShowMovementLines = !fieldControl.ShowMovementLines;
            b.Checked = fieldControl.ShowMovementLines;
        }

        private void fieldControl_KeyDown(object sender, KeyEventArgs e)
        {
            StopPlayingSequence();

            if (!e.Handled) {
                if ((e.KeyCode == Keys.Left) || (e.KeyCode == Keys.Up)) {
                    MoveToPreviousLayout();
                } else if ((e.KeyCode == Keys.Right) || (e.KeyCode == Keys.Down)) {
                    MoveToNextLayout();
                }
            }
        }

        private int RecordPositionToSequence(bool replace, int index, LayoutSequence sequence)
        {
            FieldLayout layout = fieldControl.FieldLayout;
            fieldControl.IsDirty = false;
            sequenceDirty = true;
            if (replace) {
                sequence.SetLayout(index, layout);
                return index;
            } else {
                return sequence.AddNewLayout(index, layout);
            }
        }

        private void RestorePositionFromSequence(int index, LayoutSequence sequence, FieldLayout _nextLayout)
        {
            fieldControl.SetLayouts(sequence.GetLayout(index), _nextLayout);
            fieldControl.IsDirty = false;
        }


        private void NotImplementedYet()
        {
            string msg = Properties.Resources.ResourceManager.GetString("NotImplementedYet");
            GlobalizationAwareMessageBox.Show(
              this,
              msg,
              this.Text,
              MessageBoxButtons.OK,
              MessageBoxIcon.Information,
              MessageBoxDefaultButton.Button1,
              (MessageBoxOptions)0);
        }


        private void MainForm_Shown(object sender, EventArgs e)
        {
            UpdateFileMenuItems();

            UpdateSequenceControls();
            if (null == fieldControl.FieldType)
            {
                FileNew(true, false);
            }
        }

        private void newPlayingSurfaceTypeMenuItem_Click(object sender, EventArgs e)
        {
            StopPlayingSequence();

            FileNew(false, true);
        }

        private void RenderToImage(Bitmap bitmap, FieldLayout layout, FieldLayout nextLayout)
        {
            FieldLayout nl = null;
            if (fieldControl.ShowMovementLines) {
                nl = nextLayout;
            }
            fieldControl.DrawIntoImage(bitmap, layout, nl);
        }

        private void copyMenuItem_Click(object sender, EventArgs e)
        {
            StopPlayingSequence();

            Clipboard.Clear();
            using (Bitmap bitmap = new Bitmap(fieldControl.Width, fieldControl.Height)) {
                RenderToImage(bitmap, fieldControl.FieldLayout, GetNextLayout());
                Clipboard.SetImage(bitmap);
            }
        }

        private void SaveSequenceEntryToFile(string imageFileName, FieldLayout layout, FieldLayout nextLayout, System.Drawing.Imaging.ImageFormat imageFormat)
        {
            using (Bitmap bitmap = new Bitmap(fieldControl.Width, fieldControl.Height)) {
                RenderToImage(bitmap, layout, nextLayout);
                bitmap.Save(imageFileName, imageFormat);
            }
        }

        private void SaveImagesToFile(bool saveEntireSequence)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog()) {
                saveFileDialog.Filter = saveAsImageFileFilter;
                saveFileDialog.FilterIndex = 3; // set to PNG by default
                saveFileDialog.RestoreDirectory = true;
                saveFileDialog.OverwritePrompt = !saveEntireSequence;
                if (saveEntireSequence) {
                    saveFileDialog.Title = Properties.Resources.ResourceManager.GetString("SaveImageSequenceDialogTitle");
                } else {
                    saveFileDialog.Title = Properties.Resources.ResourceManager.GetString("SaveImageDialogTitle");
                }
                DialogResult res = saveFileDialog.ShowDialog();
                if (res != DialogResult.OK) {
                    return;
                }

                System.Drawing.Imaging.ImageFormat imageFormat = System.Drawing.Imaging.ImageFormat.Png;
                switch (saveFileDialog.FilterIndex) {
                    case 1:
                        imageFormat = System.Drawing.Imaging.ImageFormat.Bmp;
                        break;
                    case 2:
                        imageFormat = System.Drawing.Imaging.ImageFormat.Gif;
                        break;
                    case 3:
                    default:
                        imageFormat = System.Drawing.Imaging.ImageFormat.Png;
                        break;
                    case 4:
                        imageFormat = System.Drawing.Imaging.ImageFormat.Jpeg;
                        break;
                }

                if (saveEntireSequence) {
                    string fileNamePattern = saveFileDialog.FileName;
                    int idx = fileNamePattern.LastIndexOf(".", StringComparison.Ordinal);
                    if (idx < 0) {
                        idx = fileNamePattern.Length;
                    }
                    fileNamePattern = fileNamePattern.Insert(idx, Properties.Resources.ResourceManager.GetString("ImageFileNamePattern"));
                    for (int sequenceIndex = 0; sequenceIndex < currentSequence.NumberOfLayouts; sequenceIndex++) {
                        string entryFileName = string.Format(CultureInfo.CurrentUICulture, fileNamePattern, sequenceIndex + 1);
                        SaveSequenceEntryToFile(entryFileName, currentSequence.GetLayout(sequenceIndex), currentSequence.GetLayout(sequenceIndex + 1), imageFormat);
                    }
                } else {
                    SaveSequenceEntryToFile(saveFileDialog.FileName, fieldControl.FieldLayout, GetNextLayout(), imageFormat);
                }
            }
        }

        private void saveSequenceToImageFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StopPlayingSequence();

            SaveImagesToFile(true);
        }

        private void exportToAviFileMenuItem_Click(object sender, EventArgs e)
        {
            StopPlayingSequence();

            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = saveAsAVIFileFilter;
                saveFileDialog.FilterIndex = 1; //Set filter as AVI
                saveFileDialog.RestoreDirectory = true;
                saveFileDialog.OverwritePrompt = true;
                saveFileDialog.Title = Properties.Resources.ResourceManager.GetString("ExportToAVIFile");

                DialogResult res = saveFileDialog.ShowDialog();
                if (res != DialogResult.OK)
                {
                    return;
                }
                AviWriter aw = new AviWriter();
                Bitmap bitmap = aw.Open(saveFileDialog.FileName,1,fieldControl.Width, fieldControl.Height);
                for (int sequenceIndex = 0; sequenceIndex < currentSequence.NumberOfLayouts; sequenceIndex++)
                {
                    RenderToImage(bitmap, currentSequence.GetLayout(sequenceIndex), currentSequence.GetLayout(sequenceIndex + 1));
                    aw.AddFrame();                    
                }
                aw.Close();
            }
        }

        private void saveTofileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StopPlayingSequence();

            SaveImagesToFile(false);
        }


        private void ShowInstalledDocument(string[] possibleDocumentNames)
        {
            string exePath = Application.ExecutablePath;
            string binDir = Path.GetDirectoryName(exePath);
            string installDir = Path.GetDirectoryName(binDir);
#if DEBUG
      string rootProjectDir = Path.GetDirectoryName(installDir);
#endif
            string[] appFolders = new string[] {
        installDir
        ,binDir
#if DEBUG
        ,rootProjectDir
#endif
      };

            string documentFileName = null;
            // Locate the document from the list of possible names
            foreach (string docName in possibleDocumentNames) {
                foreach (string appFolder in appFolders) {
                    documentFileName = Path.Combine(appFolder, docName);
                    if (!File.Exists(documentFileName)) {
                        documentFileName = null;
                    } else {
                        // exit the inner loop as we have found one that exists
                        break;
                    }
                }
                if (documentFileName != null) {
                    // exit the outer loop as we have found one that exists
                    break;
                }
            }

            // If we located a document, open it. If successful,
            // exit, otherwise catch the exceptions and display
            // a generic error message for all errors.
            if (!string.IsNullOrEmpty(documentFileName)) {
                try {
                    System.Diagnostics.Process.Start(documentFileName);
                    return;
                } catch (System.ObjectDisposedException) {
                } catch (System.InvalidOperationException) {
                } catch (System.ComponentModel.Win32Exception) {
                }
            }

            string msg = Properties.Resources.ResourceManager.GetString("UnableToOpenFile_InstallationMayBeIncomplete");
            GlobalizationAwareMessageBox.Show(
              this,
              msg,
              this.Text,
              MessageBoxButtons.OK,
              MessageBoxIcon.Exclamation,
              MessageBoxDefaultButton.Button1,
              (MessageBoxOptions)0);
            return;
        }
        private void helpMenuItem_Click(object sender, EventArgs e)
        {
            ShowInstalledDocument(new string[] { "help.txt" });
        }

    private void licenseMenuItem_Click(object sender, EventArgs e)
    {
      ShowInstalledDocument(new string[] { "license.rtf", "license.txt" });
    }

    private void readMeMenuItem_Click(object sender, EventArgs e)
    {
      ShowInstalledDocument(new string[] { "readme.txt" });
    }

    private void changeLogMenuItem_Click(object sender, EventArgs e)
    {
      ShowInstalledDocument(new string[] { "changelog.txt" });
    }

    private void StopPlayingSequence()
    {
      playSequenceTimer.Enabled = false;
      fieldControl.AllowInteraction = true;
      UpdateSequenceControls();
    }

    private void StartPlayingSequence()
    {
      playSequenceTimer.Interval = global::TacticsBoard.Properties.Settings.Default.AnimationFrameDurationInMilliseconds;
      playSequenceTimer.Enabled = true;
      fieldControl.AllowInteraction = false;
      UpdateSequenceControls();
    }

    private bool IsPlayingSequence
    {
      get { return playSequenceTimer.Enabled; }
    }

    private void goToFirstToolStripButton_Click(object sender, EventArgs e)
    {
      StopPlayingSequence();
      if (null == currentSequence) {
        return;
      }
      positionInSequence = 0;
      RestorePositionFromSequence(positionInSequence, currentSequence, GetNextLayout());
      UpdateSequenceControls();
    }


    private void PlayToolStripButton_Click(object sender, EventArgs e)
    {
      if (IsPlayingSequence) {
        StopPlayingSequence();
      } else {
        if (null == currentSequence) {
          return;
        }
        if ((currentSequence.NumberOfLayouts > 0) && (fieldControl.IsDirty)) {
          DialogResult dr =
            GlobalizationAwareMessageBox.Show(this,
                                    Properties.Resources.ResourceManager.GetString("SaveSequenceEntryBeforeSwitchingEntries"),
                                    this.Text,
                                    MessageBoxButtons.YesNoCancel,
                                    MessageBoxIcon.Question,
                                    MessageBoxDefaultButton.Button1,
                                    (MessageBoxOptions)0);
          switch (dr) {
            case DialogResult.Yes:
              RecordPositionToSequence(true, positionInSequence, currentSequence);
              break;
            case DialogResult.Cancel:
              return;
          }
        }
        if (positionInSequence + 1 >= currentSequence.NumberOfLayouts) {
          GoToStartOfSequence();
        }
        StartPlayingSequence();
      }
    }

    private void GoToStartOfSequence()
    {
      positionInSequence = 0;
      RestorePositionFromSequence(positionInSequence, currentSequence, GetNextLayout());
      UpdateSequenceControls();
    }

    private void PlaySequenceTimer_Tick(object sender, EventArgs e)
    {
      if (positionInSequence + 1 < currentSequence.NumberOfLayouts) {
        MoveToNextLayout();
        if (positionInSequence + 1 < currentSequence.NumberOfLayouts) {
          // Continue playing the sequence if there are more left
          return;
        } else if (repeatToolStripButton.Checked) {
          // Repeat is enabled, so go back to the beginning and continue playback
          GoToStartOfSequence();
          return;
        }
      }
      StopPlayingSequence();
    }

    private void GoToLastToolStripButton_Click(object sender, EventArgs e)
    {
      StopPlayingSequence();
      if (null == currentSequence) {
        return;
      }
      if (currentSequence.NumberOfLayouts > 0) {
        positionInSequence = currentSequence.NumberOfLayouts - 1;
        RestorePositionFromSequence(positionInSequence, currentSequence, GetNextLayout());
        UpdateSequenceControls();
      }
    }

    #region ICustomLabelProvider Members

    public string GetCustomLabel(string tag)
    {
      if (null != currentSequence) {
        return currentSequence.GetCustomLabel(tag);
      }
      return null;
    }

    public void UpdateCustomLabel(string tag, string label)
    {
      if (null != currentSequence) {
        currentSequence.AddOrUpdateCustomLabel(tag, label);
      }
    }

    public void RemoveCustomLabel(string tag)
    {
      if (null != currentSequence) {
        currentSequence.RemoveCustomLabel(tag);
      }
    }

    #endregion

    private void HorizontalToolStripMenuItem_Click(object sender, EventArgs e)
    {
      fieldControl.SetView(fieldControl.ZoomPoint, fieldControl.ZoomFactor, 0.0F);
      UpdateViewMenuItems();
    }

    private void VerticalToolStripMenuItem_Click(object sender, EventArgs e)
    {
      fieldControl.SetView(fieldControl.ZoomPoint, fieldControl.ZoomFactor, 90.0F);
      UpdateViewMenuItems();
    }

    private void ResetViewToolStripMenuItem_Click(object sender, EventArgs e)
    {
      fieldControl.ResetView();
      UpdateViewMenuItems();
    }

        private void UpdateViewMenuItems()
    {
      horizontalToolStripMenuItem.Checked = fieldControl.RotationAngle == 0.0F;
      verticalToolStripMenuItem.Checked = fieldControl.RotationAngle == 90.0F;
    }


        private void MainForm_Load(object sender, EventArgs e)
        {

        }

       
    }
}