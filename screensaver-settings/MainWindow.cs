/* This file is part of screensaver-settings
   Copyright(C) 2008 xFX JumpStart
 
   screensaver-settings is free software; you can redistribute it and/or
   modify it under the terms of the GNU Lesser General Public License as
   published by the Free Software Foundation; either version 3 of the
   License, or(at your option) any later version.
 
   screensaver-settings is distributed in the hope that it will be useful,
   but WITHOUT ANY WARRANTY; without even the implied warranty of
   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Lesser
   General Public License for more details.

   You should have received a copy of the GNU Lesser General Public License
   along with screensaver-settings; if not, write to the Free Software
   Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston,
   MA 02110-1301 USA */
using System;
using Gtk;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using Gnome;

public partial class MainWindow : Window {
	static string GNOME_SESSION_DIR = "/desktop/gnome/session";
	static string GCONF_APP_PATH = "/apps/gnome-screensaver";
	static string IDLEDELAY_KEY = GNOME_SESSION_DIR + "/idle_delay";
	static string IDLEACTIVATION_KEY = GCONF_APP_PATH + "/idle_activation_enabled";
	static string LOCKSCREEN_KEY = GCONF_APP_PATH + "/lock_enabled";
	static string MODE_KEY = GCONF_APP_PATH + "/mode";
	static string THEMES_KEY = GCONF_APP_PATH + "/themes";
	static string CYCLEDELAY_KEY = GCONF_APP_PATH + "/cycle_delay";

	enum OperationModeConstants {
		BlankScreen = 0,
		OneScreenSaver = 1,
		Random = 2
	}
	private OperationModeConstants opMode;
	private List<ScreenSaver> screenSavers = new List<ScreenSaver>();
	private ScreenSaver selectedScreenSaver;
	private ulong previewWindowID = 0;
	private Process previewProcess;
	private SettingsUI settingsWin;
	private FullscreenPreviewWindow fullScreenWin;
	private GConf.Client client;
	private bool useKillAll;

	public MainWindow() : base(WindowType.Toplevel) {
		this.Build();
		
		btnClose.Clicked += CloseApp;
		btnSettings.Clicked += ShowSettingsWindow;
		btnFullscreen.Clicked += ToggleFullScreen;
		btnPowerManagement.Clicked += OpenPowerManagement;
		btnHelp.Clicked += ShowHelp;
		
		this.DeleteEvent += TerminateApp;
		
		//this.Build() issues this.Show(); thus, we're already visible by the
		//time we hit here.
		Init();
	}

	private void TerminateApp(object sender, DeleteEventArgs a) {
		CloseApp(this, new EventArgs());
	}

	private void LoadScreenSavers(bool showCheckBoxes) {
		int colNameID = (showCheckBoxes ? 1 : 0);
		
		// hmmm... where's the tvScreenSavers.Columns.Clear() ???
		TreeViewColumn[] c = tvScreenSavers.Columns;
		for(int i = 0; i < c.Length; i++) {
			tvScreenSavers.RemoveColumn(c[i]);
		}
		
		ListStore ssStore = showCheckBoxes ? new ListStore(typeof(bool), typeof(string)) : new ListStore(typeof(string));
		tvScreenSavers.Model = ssStore;
		
		if(showCheckBoxes) {
			CellRendererToggle crToggle = new CellRendererToggle();
			crToggle.Activatable = true;
			crToggle.Toggled += SSToggled;
			tvScreenSavers.AppendColumn("CheckState", crToggle, "active", 0);
		}
		
		tvScreenSavers.HeadersVisible = false;
		tvScreenSavers.AppendColumn("Name", new CellRendererText(), "text", colNameID);
		tvScreenSavers.Selection.Changed += ScreenSaverSelected;
		tvScreenSavers.Selection.Mode = SelectionMode.Single;
		//tvScreenSavers.RowSeparatorFunc = new TreeViewRowSeparatorFunc(TreeViewSeparator, null);
		
		ssStore.SetSortFunc(colNameID, new TreeIterCompareFunc(SortList));
		ssStore.SetSortColumnId(colNameID, SortType.Ascending);
		
		ScreenSaver systemSelectedScreensaver = null;
		string[] themes;
		try {
			themes = (string[])client.Get(THEMES_KEY);
		} catch {
			themes = new string[] {};
		}
		string[] files = Directory.GetFiles(MainClass.SS_THEME_PATH, "*.desktop");
		
		screenSavers.Clear();
		foreach(string fileName in files) {
			DesktopItem desktopTheme;
			
			FileInfo f = new FileInfo(fileName);
			string userTheme = System.IO.Path.Combine(ScreenSaver.UserDesktopPath, f.Name);
			if(File.Exists(userTheme))
				desktopTheme = DesktopItem.NewFromFile(userTheme, DesktopItemLoadFlags.NoTranslations);
			else
				desktopTheme = DesktopItem.NewFromFile(fileName, DesktopItemLoadFlags.NoTranslations);
			
			if(desktopTheme == null) {
				//TODO: something better than silently fail to load theme
				continue;
			}
			TextReader configData;
			if(File.Exists(ScreenSaver.GetConfigFile(desktopTheme))) {
				configData = File.OpenText(ScreenSaver.GetConfigFile(desktopTheme));
			} else {
				configData = null;
			}
			ScreenSaver ss = new ScreenSaver(desktopTheme, configData);
			if(ss.IsValid) {
				foreach(string theme in themes) {
					if(theme == ss.GConfName) {
						ss.Selected = true;
						if(systemSelectedScreensaver == null)
							systemSelectedScreensaver = ss;
						break;
					}
				}
				screenSavers.Add(ss);
				if(showCheckBoxes) {
					ssStore.AppendValues(ss.Selected, ss.Title);
				} else {
					ssStore.AppendValues(ss.Title);
				}
			}
		}
		
		tvScreenSavers.ColumnsAutosize();
		if(screenSavers.Count > 0) {
			//This seems to prevent Xgl from crashing on my machine.
			System.Threading.Thread.Sleep(1000);
			bool forceSelectFirst = true;
			TreeIter iter;
			for(int i = 0; i < ssStore.IterNChildren(); i++) {
				ssStore.GetIter(out iter, new TreePath(i.ToString()));
				if(systemSelectedScreensaver != null && (string)ssStore.GetValue(iter, colNameID) == systemSelectedScreensaver.Name) {
					TreePath iterPath = ssStore.GetPath(iter);
					tvScreenSavers.Selection.SelectPath(iterPath);
					tvScreenSavers.ScrollToCell(iterPath, tvScreenSavers.Columns[colNameID], false, 0, 0);
					forceSelectFirst = false;
					break;
				}
			}
			if(forceSelectFirst) {
				tvScreenSavers.Selection.SelectPath(new TreePath("0"));
			}
		} else {
			SetControlsState(false, opMode);
		}
	}

	private bool TreeViewSeparator(TreeModel model, TreeIter iter) {
		int colNameID = tvScreenSavers.Columns.Length - 1;
		
		return(string)model.GetValue(iter, colNameID) == "---";
	}

	private int SortList(TreeModel model, TreeIter iter1, TreeIter iter2) {
		int colNameID = tvScreenSavers.Columns.Length - 1;
		
		string title1 = (string)model.GetValue(iter1, colNameID);
		string title2 = (string)model.GetValue(iter2, colNameID);
		
		return title1.CompareTo(title2);
	}

	private void SSToggled(object sender, ToggledArgs e) {
		TreeIter iter;
		TreeModel model = tvScreenSavers.Model;
		
		if(model.GetIter(out iter, new TreePath(e.Path))) {
			bool newState = !(bool)model.GetValue(iter, 0);
			model.SetValue(iter, 0, newState);
			
			ScreenSaverSelected((string)model.GetValue(iter, 1), true, newState);
		}
	}

	private void ScreenSaverSelected(object sender, EventArgs e) {
		TreeIter iter;
		TreeModel model;
		TreeSelection sel = (TreeSelection)sender;
		int colNameID = tvScreenSavers.Columns.Length - 1;
		
		if(sel.GetSelected(out model, out iter))
			ScreenSaverSelected((string)model.GetValue(iter, colNameID), false, false);
	}

	private void ScreenSaverSelected(string title, bool toggleCheckState, bool checkedState) {
		if(selectedScreenSaver != null && selectedScreenSaver.HasChanged) {
			selectedScreenSaver.ApplyCommandLine();
		}
		
		StopPreview();
		
		selectedScreenSaver = null;
		if(selectedScreenSaver != null) {
			btnSettings.Sensitive = (opMode != OperationModeConstants.BlankScreen) && selectedScreenSaver.HasConfig;
		} else {
			btnSettings.Sensitive = (opMode != OperationModeConstants.BlankScreen);
		}
		btnFullscreen.Sensitive = (opMode != OperationModeConstants.BlankScreen);
		
		if(opMode != OperationModeConstants.BlankScreen) {
			foreach(ScreenSaver ss in screenSavers) {
				if(ss.Title == title) {
					selectedScreenSaver = ss;
					StartPreview();
					if(settingsWin.GdkWindow == null)
						CreateSettingsWindow();
					if(settingsWin.GdkWindow.IsVisible)
						ShowSSSettings();
					
					btnSettings.Sensitive = ss.HasConfig;
					btnFullscreen.Sensitive = true;
					if(fullScreenWin != null)
						fullScreenWin.OptionsButtonEnabled = ss.HasConfig;
					
					if(opMode == OperationModeConstants.OneScreenSaver)
						ss.Selected = true;
					else if(toggleCheckState) {
						selectedScreenSaver.Selected = checkedState;
					}
					SaveScreensaversSelection();
				} else if(opMode == OperationModeConstants.OneScreenSaver)
					ss.Selected = false;
			}
		}
	}

	private void ShowSettingsWindow(object sender, EventArgs e) {
		ShowSSSettings();
	}

	public void HideSettingsWindow() {
		if(settingsWin != null)
			settingsWin.Hide();
	}

	private void ToggleFullScreen(object sender, EventArgs e) {
		StopPreview();
		HideSettingsWindow();
		
		fullScreenWin = new FullscreenPreviewWindow();
		previewWindowID = MainClass.GetPreviewWindowID(fullScreenWin.PreviewArea);
		fullScreenWin.Init(this);
		fullScreenWin.OptionsButtonEnabled = selectedScreenSaver.HasConfig;
		fullScreenWin.Modal = true;
		fullScreenWin.Fullscreen();
		fullScreenWin.Destroyed += ExitedFullscreen;
		
		StartPreview();
	}

	private void ExitedFullscreen(object sender, EventArgs e) {
		fullScreenWin = null;
		StopPreview();
		
		previewWindowID = MainClass.GetPreviewWindowID(ssPreviewArea);
		StartPreview();
	}

	public void ShowSSSettings() {
		if(fullScreenWin != null)
			fullScreenWin.Destroy();
		
		settingsWin.Show();
		settingsWin.CreateUI(selectedScreenSaver);
	}

	private void OpenPowerManagement(object sender, EventArgs e) {
		Process p = new Process();
		if(File.Exists("/usr/bin/gnome-power-preferences")) {
			p.StartInfo.FileName = "/usr/bin/gnome-power-preferences";
		} else {
			p.StartInfo.FileName = "/usr/bin/gnome-control-center";
			p.StartInfo.Arguments = "power";
		}
		p.Start();
		
		//p.WaitForExit();
	}

	private void ShowHelp(object sender, EventArgs e) {
		string msg = "";
		/*
		string readmeFile = String.Format("{0}README", System.Reflection.Assembly.GetExecutingAssembly().Location.Replace("screensaver-settings.exe", ""));
		FileInfo file = new FileInfo(readmeFile);
		if(file.Exists) {
			StreamReader sr = file.OpenText();
			msg = Environment.NewLine + sr.ReadToEnd().Replace("<", "&lt;");
			sr.Close();
		}settingsWin.Hide();
		*/		
		Dialog dlg = new MessageDialog(this, DialogFlags.DestroyWithParent, MessageType.Info, ButtonsType.Close, "This feature has not been implemented yet.\nYou are on your own!" + msg);
		dlg.Run();
		dlg.Destroy();
	}

	private void CloseApp(object sender, EventArgs e) {
		this.Sensitive = false;
		StopPreview();
		Application.Quit();
	}

	private string[] GetEnvVars() {
		List<string > vars = new List<string>();
		string[] requiered = { "PATH", "SESSION_MANAGER", "XAUTHORITY", "XAUTHLOCALHOSTNAME", "LANG", "LANGUAGE", "DBUS_SESSION_BUS_ADDRESS" };
		
		vars.Add("HOME=" + Environment.GetFolderPath(Environment.SpecialFolder.Personal));
		vars.Add("DISPLAY=" + ssPreviewArea.Display.Name);
		vars.Add("XSCREENSAVER_WINDOW=" + previewWindowID);
		
		for(int i = 0; i < requiered.Length; i++) {
			string value = Environment.GetEnvironmentVariable(requiered[i]);
			if(value != null) {
				vars.Add(requiered[i] + "=" + value);
			}
		}
		return vars.ToArray();
	}

	private void StartPreview() {
		StopPreview();
		if(selectedScreenSaver == null)
			return;
		
		try {
			previewProcess = new Process();
			
//			Console.WriteLine();
//			Console.WriteLine(String.Format("{0} {1} {2} 0x{3:X}", selectedScreenSaver.Binary, selectedScreenSaver.CommandLineOptions, MainClass.WIN_REDIR, previewWindowID));
//			Console.WriteLine();
//			
			previewProcess.StartInfo.FileName = selectedScreenSaver.BinaryFullFileName;
			
			previewProcess.StartInfo.RedirectStandardOutput = true;
			previewProcess.StartInfo.RedirectStandardError = true;
			previewProcess.StartInfo.UseShellExecute = false;
			previewProcess.StartInfo.Arguments = "--help";
			previewProcess.Start();
			string output = previewProcess.StandardOutput.ReadToEnd() + previewProcess.StandardError.ReadToEnd();
			previewProcess.WaitForExit();
			
			// TODO: All screensavers should be started using the Gdk.Spawn.OnScreen() method.
			// Need to investigate why those that support -window-id do not like that method...
			if(output.Contains(MainClass.WIN_REDIR)) {
				previewProcess.StartInfo.Arguments = String.Format("{0} {1} {2}", selectedScreenSaver.CommandLineOptions, MainClass.WIN_REDIR, previewWindowID);
				previewProcess.Start();
				useKillAll = false;
			} else {
				bool result;
				int childPId;
				
				List<string > arg = new List<string>();
				arg.Add(selectedScreenSaver.BinaryFullFileName);
				if(selectedScreenSaver.CommandLineOptions != "")
					arg.Add(selectedScreenSaver.CommandLineOptions);

				// FIXME: Spawn is not defined???? WTF!?
				result = Gdk.Spawn.OnScreen(ssPreviewArea.Screen, null, arg.ToArray(), GetEnvVars(), GLib.SpawnFlags.SearchPath | GLib.SpawnFlags.DoNotReapChild | GLib.SpawnFlags.StderrToDevNull | GLib.SpawnFlags.StdoutToDevNull, null, out childPId);
				
				if(result)
					previewProcess = Process.GetProcessById(childPId);
				useKillAll = true;
			}
			
			if(fullScreenWin != null) {
				fullScreenWin.ScreensaverTitle = selectedScreenSaver.Title;
			}
		} catch(Exception e) {
			Console.WriteLine(e.Message);
		}
	}

	private void StopPreview() {
		//Console.WriteLine("Stopping Screensaver Preview");
		
		// FIXME: Clear() is not defined
		//ssPreviewArea.GdkWindow.Clear();
		ssPreviewArea.GdkWindow.Background = Style.Black;
		
		if(previewProcess != null) {
			if(useKillAll) {
				// FIXME: Screensavers executed using the OnScreen() method cannot beclosed with the usual Process.Kill() method... for some odd reason.
				// So we use "killall" to terminate the screensaver's process. Ugly... but it works.
				try {
					string fileName = selectedScreenSaver.BinaryFullFileName;
					if(fileName.Contains("/")) {
						string[] tokens = fileName.Split('/');
						fileName = tokens[tokens.Length - 1];
					}
					
					Process killAllProcess = new Process();
					killAllProcess.StartInfo.FileName = "killall";
					killAllProcess.StartInfo.UseShellExecute = false;
					killAllProcess.StartInfo.Arguments = "-e " + fileName;
					killAllProcess.Start();
					killAllProcess.WaitForExit();
					killAllProcess.Dispose();
				} catch(Exception e) {
					Console.WriteLine(e.Message);
				}
			} else {
				if(!previewProcess.HasExited) {
					try {
						previewProcess.Kill();
					} catch(Exception e) {
						Console.WriteLine(e.Message);
					}
				}
			}
			previewProcess.Dispose();
			previewProcess = null;
		}
	}

	public void SelectNextSS() {
		TreeIter iter;
		TreeModel model;
		TreeSelection sel = tvScreenSavers.Selection;
		
		if(sel.GetSelected(out model, out iter)) {
			int selIndex = model.GetPath(iter).Indices[0];
			tvScreenSavers.Selection.SelectPath(new TreePath((selIndex + 1).ToString()));
		}
	}

	public void SelectPreviousSS() {
		TreeIter iter;
		TreeModel model;
		TreeSelection sel = tvScreenSavers.Selection;
		
		if(sel.GetSelected(out model, out iter)) {
			int selIndex = model.GetPath(iter).Indices[0];
			tvScreenSavers.Selection.SelectPath(new TreePath((selIndex - 1).ToString()));
		}
	}

	private void HandleControlsChanged(object sender, EventArgs e) {
		UpdateControls();
	}

	private void UpdateControls() {
		OperationModeConstants mode = (OperationModeConstants)cmbMode.Active;
		SetControlsState(chkActivateSS.Active, mode);
		
		if(opMode == mode)
			return;
		opMode = mode;
		
		switch(opMode) {
		case OperationModeConstants.BlankScreen:
			StopPreview();
			break;
		case OperationModeConstants.OneScreenSaver:
			LoadScreenSavers(false);
			break;
		case OperationModeConstants.Random:
			LoadScreenSavers(true);
			break;
		}
		
		hbSSButtons.Sensitive &= (selectedScreenSaver != null);
	}

	private void SetControlsState(bool state, OperationModeConstants mode) {
		bool forceDisabled = (mode == OperationModeConstants.BlankScreen);
		
		hsIdleDelay.Sensitive = state;
		chkLockScreen.Sensitive = state;
		//hbModeControls.Sensitive = state;
		hbRandomControls.Sensitive = state;
		hbRandomControls.Visible = (mode == OperationModeConstants.Random);
		hpCycleDelay.Visible = hbRandomControls.Visible;
		hbSelectionOptions.Visible = hbRandomControls.Visible;
		swScreensavers.Sensitive = (state ? !forceDisabled : state);
		hbSSButtons.Sensitive = state;
		if(!state) {
			StopPreview();
		} else if(opMode != OperationModeConstants.BlankScreen) {
			StartPreview();
		}
	}

	private void Init() {
		previewWindowID = MainClass.GetPreviewWindowID(ssPreviewArea);
		CreateSettingsWindow();
		
		this.BorderWidth = (uint)10;
		
		client = new GConf.Client();
		GetSettingsFromGConf();
		client.AddNotify(GCONF_APP_PATH, new GConf.NotifyEventHandler(GConfSettingsChanged));
		
		hsCycleDelay.SetRange(1, 120);
		
		GetScreensaverFromGConf(true);
		cmbMode.Active = (int)opMode;
		LoadScreenSavers(opMode == OperationModeConstants.Random);
		UpdateControls();
		
		hsIdleDelay.ValueChanged += SaveSettingsToGConf;
		chkActivateSS.Toggled += SaveSettingsToGConf;
		chkActivateSS.Toggled += HandleControlsChanged;
		chkLockScreen.Toggled += SaveSettingsToGConf;
		cmbMode.Changed += HandleControlsChanged;
		cmbMode.Changed += SaveSettingsToGConf;
		hsCycleDelay.ValueChanged += SaveSettingsToGConf;
		btnSelAll.Clicked += ApplySelection;
		btnSelNone.Clicked += ApplySelection;
		btnSelInv.Clicked += ApplySelection;
	}

	private void CreateSettingsWindow() {
		settingsWin = new SettingsUI();
		settingsWin.Hide();
		settingsWin.SettingsChanged += StartPreview;
		settingsWin.WindowPosition = WindowPosition.Center;
	}

	private void GetSettingsFromGConf() {
		try {
			hsIdleDelay.Value = (int)client.Get(IDLEDELAY_KEY);
		} catch(Exception e) {
			Console.WriteLine(IDLEDELAY_KEY + " " + e.Message);
		}
		
		try {
			chkActivateSS.Active = (bool)client.Get(IDLEACTIVATION_KEY);
		} catch(Exception e) {
			Console.WriteLine(IDLEACTIVATION_KEY + " " + e.Message);
		}
		
		try {
			chkLockScreen.Active = (bool)client.Get(LOCKSCREEN_KEY);
		} catch(Exception e) {
			Console.WriteLine(LOCKSCREEN_KEY + " " + e.Message);
		}
		
		try {
			int value = 0;
			int.TryParse(client.Get(CYCLEDELAY_KEY).ToString(), out value);
		} catch(Exception e) {
			Console.WriteLine(CYCLEDELAY_KEY + " " + e.Message);
		}
	}

	private void SaveSettingsToGConf(object sender, EventArgs e) {
		client.Set(IDLEDELAY_KEY, (int)hsIdleDelay.Value);
		client.Set(IDLEACTIVATION_KEY, chkActivateSS.Active);
		client.Set(LOCKSCREEN_KEY, chkLockScreen.Active);
		
		switch(opMode) {
		case OperationModeConstants.BlankScreen:
			client.Set(MODE_KEY, "blank-only");
			break;
		case OperationModeConstants.Random:
			client.Set(CYCLEDELAY_KEY, hsCycleDelay.Value);
			client.Set(MODE_KEY, "random");
			break;
		default:
			client.Set(MODE_KEY, "single");
			break;
		}
		
		lblIdleDelayValue.LabelProp = IntToTime((int)hsIdleDelay.Value);
		lblIdleDelayValue.Xalign = (float)(hsIdleDelay.Value / hsIdleDelay.Adjustment.Upper);
		
		lblCycleDelayValue.LabelProp = IntToTime((int)hsCycleDelay.Value);
		lblCycleDelayValue.Xalign = (float)(hsCycleDelay.Value / hsCycleDelay.Adjustment.Upper);
	}

	private string IntToTime(int value) {
		TimeSpan t = TimeSpan.FromMinutes(value);
		int h = t.Hours;
		int m = t.Minutes;
		
		string hs = h > 0 ? h.ToString() + " hour" + (h > 1 ? "s" : "") : "";
		string ms = m > 0 ? m.ToString() + " minute" + (m > 1 ? "s" : "") : "";
		
		return hs + " " + ms;
	}

	private void SaveScreensaversSelection() {
		if(opMode == OperationModeConstants.BlankScreen)
			client.Set(THEMES_KEY, new string[] { "" });
		else {
			List<string > names = new List<string>();
			foreach(ScreenSaver ss in screenSavers)
				if(ss.Selected)
					names.Add(ss.GConfName);
			client.Set(THEMES_KEY, names.ToArray());
		}
	}

	private string GetScreensaverFromGConf(bool isFirstTime) {
		string name = "";
		try {
			name = ((string)client.Get(MODE_KEY)).ToLower();
		} catch {
			name = "blank-only";
		}
		
		switch(name) {
		case "blank-only":
			if(isFirstTime)
				opMode = OperationModeConstants.BlankScreen;
			return "Blank Screen";
		case "random":
			if(isFirstTime)
				opMode = OperationModeConstants.Random;
			return "Random";
		}
		
		try {
			name = ((string[])client.Get(THEMES_KEY))[0];
			if(isFirstTime)
				opMode = OperationModeConstants.OneScreenSaver;
			return name.Remove(0, "screensavers-".Length).ToLower();
		} catch {
			if(isFirstTime)
				opMode = OperationModeConstants.BlankScreen;
			return "blank-only";
		}
	}

	public void GConfSettingsChanged(object sender, GConf.NotifyEventArgs args) {
		GetSettingsFromGConf();
	}
	
	private void ApplySelection(object sender, EventArgs e) {
		const int SelAll = 0;
		const int SelNone = 1;
		const int SelInv = 2;
		
		int mode = SelAll;
		switch(((Button)sender).Name) {
		case "btnSelAll":
			mode = SelAll;
			break;
		case "btnSelNone":
			mode = SelNone;
			break;
		case "btnSelInv":
			mode = SelInv;
			break;
		}
		
		TreeIter iter;
		TreeModel model = tvScreenSavers.Model;
		bool state = false;
		string title;
		
		for(int i = 0; i < tvScreenSavers.Model.IterNChildren(); i++) {
			model.GetIter(out iter, new TreePath(i.ToString()));
			switch(mode) {
			case SelAll:
				state = true;
				break;
			case SelNone:
				state = false;
				break;
			case SelInv:
				state = !(bool)model.GetValue(iter, 0);
				break;
			}			
			model.SetValue(iter, 0, state);
			title = (string)model.GetValue(iter, 1);
			
			foreach(ScreenSaver ss in screenSavers) {
				if(ss.Title == title)
					ss.Selected = state;
			}						
		}
		
		SaveScreensaversSelection();
	}
}