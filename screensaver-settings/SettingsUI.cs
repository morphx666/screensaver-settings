/* This file is part of screensaver-settings
   Copyright (C) 2008 xFX JumpStart
 
   screensaver-settings is free software; you can redistribute it and/or
   modify it under the terms of the GNU Lesser General Public License as
   published by the Free Software Foundation; either version 3 of the
   License, or (at your option) any later version.
 
   screensaver-settings is distributed in the hope that it will be useful,
   but WITHOUT ANY WARRANTY; without even the implied warranty of
   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Lesser
   General Public License for more details.

   You should have received a copy of the GNU Lesser General Public License
   along with screensaver-settings; if not, write to the Free Software
   Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston,
   MA 02110-1301 USA */

using System;
using System.Xml;
using Gtk;
using System.Collections.Generic;
using System.Threading;
	
public partial class SettingsUI : Window {
	private ScreenSaver ss;
	private Timer tmr;
	private Entry tbCommand;
	
	//FIXME: This should be converted into a structure
	private struct settingsData {
		public string Arg;
		public bool IsDecimal;
		public bool Invert;
		public double Max;
		public double Min;
		
		public settingsData(string arg) {
			Arg = arg;
			IsDecimal = false;
			Invert = false;
			Min = 0F;
			Max = 0F;
		}
		
		public settingsData(string arg, bool invert) {
			Arg = arg;
			IsDecimal = false;
			Invert = invert;
			Min = 0F;
			Max = 0F;
		}
		
		public settingsData(string arg, bool isDecimal, bool invert, double min, double max) {
			Arg = arg;
			IsDecimal = isDecimal;
			Invert = invert;
			Min = min;
			Max = max;
		}
	}
	private Dictionary<string, settingsData> ssSettings;
	
	private const int spacerHeight = 6;
	
	public delegate void SettingsChangedEventHandler();
	public event SettingsChangedEventHandler SettingsChanged;
	
	public SettingsUI() : base(WindowType.Toplevel) {
		this.Build();
	}
	
	public void CreateUI(ScreenSaver screensaver) {
		//FIXME: There has to be a better way to delete all the controls/widgets
		try {
			if(this.Child != null) {
				this.Child.Destroy();
			}
		} catch {;}
		
		ss = screensaver;		
		this.Title = ss.Title + " Properties";
		
		ssSettings = new Dictionary<string, settingsData>();
		
		VBox container = new VBox(false, 3);
		container.BorderWidth = 6;
		AddTitle(ss.Title, container);		
		if(ss.HasConfig) ParseNodes(ss.UIData, container);
		AddControls(container);
		
		this.Add(container);
		this.Child.ShowAll();
		
		this.Foreach(new Callback(ApplyChangeEvent));
		tbCommand.Text = ss.CommandLineOptions;
		
		int w;
		int h;
		this.GetSize(out w, out h);
		this.Resize(1, 1);
		
		if(tmr == null) tmr = new Timer(NotifyEvent);
	}
	
	private void AddControls(Container container) {	
		Label lbl = AddSpacer(container, spacerHeight);
		Box.BoxChild bc = (Box.BoxChild)container[lbl];
		bc.Expand = true;
		
		if(ss.HasConfig) {
			HSeparator sep = new HSeparator();
			container.Add(sep);
			DontExpand(container, sep);
		}
		
		AddSpacer(container, spacerHeight);
			
		HButtonBox hb = new HButtonBox();
		//HBox hb = new Gtk.HBox(false, 0);
		hb.Spacing = 12;

        Button btnDesc = new Button();
        btnDesc.UseStock = true;
        btnDesc.Label = "gtk-about";
		btnDesc.Clicked += ShowDesc;
		btnDesc.Sensitive = (ss.Description != "");
        hb.Add(btnDesc);
		
        HButtonBox hbOptions = new HButtonBox();
        hbOptions.Name = "hbOptions";
		
		Button btnDefaults = new Button();
        btnDefaults.Label = "Defaults";
		btnDefaults.Clicked += LoadDefaults;
		btnDefaults.Sensitive = ss.HasConfig;
        hbOptions.Add(btnDefaults);

        Button btnClose = new Button();
        btnClose.Label = "Close";
		btnClose.Clicked += CloseWindow;
		btnClose.UseStock = true;
		btnClose.Label = "gtk-close";
        hbOptions.Add(btnClose);
        hb.Add(hbOptions);
		
		container.Add(hb);		
		DontExpand(container, hb);
	}
	
	private void ApplyChangeEvent(Widget w) {
		if(!w.Sensitive) return;
		if(w.GetType() == typeof(HScale))		((HScale)w).ValueChanged += UpdateCommandLine;
		if(w.GetType() == typeof(ComboBox))		((ComboBox)w).Changed += UpdateCommandLine;
		if(w.GetType() == typeof(CheckButton))	((CheckButton)w).Toggled += UpdateCommandLine;
		if(w.GetType() == typeof(SpinButton))	((SpinButton)w).Changed += UpdateCommandLine;
		if(w.GetType() == typeof(Entry))		((Entry)w).Changed += UpdateCommandLine;
		
		if(w.GetType() == typeof(HBox))			((HBox)w).Foreach(new Callback(ApplyChangeEvent));
		if(w.GetType() == typeof(VBox))			((VBox)w).Foreach(new Callback(ApplyChangeEvent));
		
		// This allows us to get the values from the XML theme that were applied to the
		//	the UI but that do not yet exists on the THEME file.
		UpdateCommandLineFromWidget(w, true);
	}
		
	private void UpdateCommandLine(object w, EventArgs e) {
		UpdateCommandLineFromWidget((Widget)w, false);
	}
	
	private void UpdateCommandLineFromWidget(Widget w, bool isInit) {
		settingsData data;
		string arg = "";
		
		if(w.GetType() == typeof(CheckButton)) {
			CheckButton chk = (CheckButton)w;
			ssSettings.TryGetValue("chk" + chk.Name, out data);
			
			ss.RemoveFromCommandLine(data.Arg);
			if(data.Invert) {
				if(!chk.Active) ss.AddToCommandLine(data.Arg);
			} else {				
				if(chk.Active) ss.AddToCommandLine(data.Arg);
			}
		} else if(w.GetType() == typeof(HScale)) {
			HScale hs = (HScale)w;
			ssSettings.TryGetValue("hs" + hs.Name, out data);
			arg = data.Arg.Replace(" %", "").Trim();
			ss.RemoveFromCommandLine(arg);
			
			double val = hs.Value;
			string format = data.IsDecimal?"{1:f}":"{1:f0}";
			if(data.Invert) val = data.Max - (val - data.Min);
			                                
			ss.AddToCommandLine(String.Format("{0} " + format, arg, val));
		} else if(w.GetType() == typeof(ComboBox)) {
			ComboBox cb = (ComboBox)w;
			
			TreeIter iter;
			string prefix;			
			int i = 0;
			do {
				//It is "normal" for TreeModel.GetValue to fail if the cell is empty...
				try {
					cb.Model.GetIter(out iter, new TreePath(i.ToString()));
					prefix = "cb" + cb.Name + (string)cb.Model.GetValue(iter, 1);
					                 
					if(!ssSettings.TryGetValue(prefix + i.ToString(), out data)) break;
					ss.RemoveFromCommandLine(data.Arg);
				} catch {;}
				i++;
			} while(i < cb.Model.IterNChildren());
			
			cb.GetActiveIter(out iter);
			prefix = "cb" + cb.Name + (string)cb.Model.GetValue(iter, 1);
			ssSettings.TryGetValue(prefix + cb.Active.ToString(), out data);
			
			ss.AddToCommandLine(data.Arg);
		} else if(w.GetType() == typeof(Entry)) {
			Entry tb = (Entry)w;
			ssSettings.TryGetValue("txt" + tb.Name, out data);
			arg = data.Arg.Replace("%", "").Trim();
			ss.RemoveFromCommandLine(arg);
			if(tb.Text != "") ss.AddToCommandLine(String.Format("{0} '{1}'", arg, tb.Text));
		} else if(w.GetType() == typeof(SpinButton)) {
			SpinButton sb = (SpinButton)w;
			ssSettings.TryGetValue("sb" + sb.Name, out data);
			arg = data.Arg.Replace(" %", "").Trim();
			ss.RemoveFromCommandLine(arg);
			
			double val;
			double.TryParse(sb.Text, out val);
			string format = data.IsDecimal?"{1:f}":"{1:f0}";			
			if(data.Invert) val = data.Max - (val - data.Min);
					
			ss.AddToCommandLine(String.Format("{0} " + format, arg, val));
		} else
			return;

		if(!isInit) {
			tbCommand.Text = ss.CommandLineOptions;
			tmr.Change(500, 0);
		}
	}
	
	private void NotifyEvent(object state) {
		SettingsChanged();		
	}
	
	private void ParseNodes(XmlNodeList nodesList, Container container) {
		foreach(XmlNode n in nodesList) {
			switch(n.Name) {
			case "number":
				CreateNumber(n, container);
				break;
			case "boolean":
				CreateCheckbox(n, container);
				break;
			case "select":
				CreateCombobox(n, container);
				break;
			case "string":
				CreateTextbox(n, container);
				break;
			case "file":
				CreateFileBrowser(n, container);
				break;
			case "hgroup":
				HBox hb = new HBox(false, 12);
				ParseNodes(n.ChildNodes, hb);
				
				bool onlyContainsComboBoxes = true;
				foreach(Widget w in hb.Children) {
					if(w.GetType() == typeof(VBox)) continue;
					if(w.GetType() != typeof(ComboBox)) {
						onlyContainsComboBoxes = false;
						break;
					}
				}
				if(onlyContainsComboBoxes) {
					hb.Homogeneous = true;
					foreach(Widget w in hb.Children) {
						DoExpand(hb, w);
					}
				}
					   
				container.Add(hb);
				break;
			case "vgroup":
				VBox vb = new VBox(false, 6);
				ParseNodes(n.ChildNodes, vb);
				container.Add(vb);
				break;
			case "command":
				CreateCommandTextbox(n, container);
				break;
			}
		}
	}
	
	private Label AddSpacer(Container container, int height) {
		// FIXME: Kind of lame way of adding a small margin...
		Label lbl = AddLabel("<small> </small>", container, false);
		lbl.HeightRequest = height;
		DontExpand(container, lbl);
		return lbl;
	}
	
	private void AddTitle(string title, Container container) {
		//AddSpacer(container);
		
		AddLabel("<big><b>" + title + "</b></big>", container, true);
		
		if(ss.IsReadOnly) {
			AddLabel("<small><span foreground='red'>Read-only mode</span></small>", container, true);
		} else {
			AddSpacer(container, spacerHeight);
		}
		HSeparator sep = new HSeparator();
		container.Add(sep);	
		DontExpand(container, sep);
		
		AddSpacer(container, spacerHeight);
	}
			
	private void CreateNumber(XmlNode n, Container container) {
		switch(Value2String(n, "type")) {
		case "slider":
			CreateSlider(n, container);
			break;
		case "spinbutton":
			CreateSpinButton(n, container);
			break;
		}
	}
	
	private Label AddLabel(string text, Container container, bool leftAlign) {
		Label lbl = new Label();
		lbl.Text = text;
		lbl.UseMarkup = true;
		container.Add(lbl);

		if(leftAlign) lbl.Xalign = 0;
		DontExpand(container, lbl);
		
		return lbl;
	}

	private void CreateSlider(XmlNode n, Container container) {
		bool usingSubContainer = false;
		Label lbl;
		Container sc;
		//if(container.GetType() == typeof(HBox)) {
			sc = new VBox(false, 0);
			usingSubContainer = true;
		//} else
		//	sc = container;
		((VBox)sc).Spacing = -3;
		
		AddSpacer(sc, spacerHeight);
		lbl = AddLabel(Value2String(n, "_label"), sc, true);

		HScale sb = new HScale(null);
		sb.Name = Value2String(n, "id") + UniqueID();
		sb.SetRange(Value2Int(n, "low"), Value2Int(n, "high"));
		sb.Value = (int)GetCurrentValue(Value2String(n, "arg"), Value2Int(n, "default"));
		sb.DrawValue = false;
		sb.CanFocus = true;
		ssSettings.Add("hs" + sb.Name, new settingsData(
		                                         Value2String(n, "arg"),
		                                         Value2String(n, "low").Contains("."),
		                                         (Value2String(n, "convert") == "invert"),
		                                         Value2Int(n, "low"),
		                                         Value2Int(n, "high")
		                                         )
		               );
		if(Value2String(n, "convert") == "invert") {
			sb.Value = Value2Int(n, "high") - (sb.Value - Value2Int(n, "low"));
		}
		sc.Add(sb);		
		DontExpand(sc, sb);

		Box.BoxChild bc;
		HBox hb = new HBox();
		
		lbl = AddLabel("<small>" + Value2String(n, "_low-label") + "</small>", hb, true);
		lbl.Yalign = 0;
		lbl.Xalign = 0;//0.01F;
		bc = (Box.BoxChild)hb[lbl];
		bc.Position = 1;
				
		lbl = AddLabel("<small>" + Value2String(n, "_high-label") + "</small>", hb, false);
		lbl.Xalign = 1;//0.99F;
		bc = (Box.BoxChild)hb[lbl];
		bc.Position = 2;
		
		bc.Expand = true;
		bc.Fill = true;
		
		sc.Add(hb);		
		DontExpand(sc, hb);
		
		AddSpacer(sc, spacerHeight);
		
		if(usingSubContainer) container.Add(sc);
	}
	
	private void CreateCheckbox(XmlNode n, Container container) {
		CheckButton chk = new CheckButton();
		chk.Name = Value2String(n, "id") + UniqueID();
		chk.CanFocus = true;
        chk.Label = Value2String(n, "_label");
        chk.DrawIndicator = true;
        chk.UseUnderline = true;
		if(Value2String(n, "arg-unset") != "") {
			ssSettings.Add("chk" + chk.Name, new settingsData(Value2String(n, "arg-unset"), true));
			chk.Active =  !GetCurrentValue(Value2String(n, "arg-unset"), false);
		} else {
			ssSettings.Add("chk" + chk.Name, new settingsData(Value2String(n, "arg-set"), false));
			chk.Active =  GetCurrentValue(Value2String(n, "arg-set"), false);
		}
		
		container.Add(chk);
		DontExpand(container, chk);
	}
	
	private void CreateTextbox(XmlNode n, Container container) {
		AddLabel(Value2String(n, "_label"), container, true);
		         
		Entry tb = new Entry();
		tb.Name = Value2String(n, "id") + UniqueID();
		tb.Text = GetCurrentValue(Value2String(n, "arg"), "");
		ssSettings.Add("txt" + tb.Name, new settingsData(Value2String(n, "arg")));
		container.Add(tb);
		DontExpand(container, tb);
	}
	
	private void CreateCommandTextbox(XmlNode n, Container container) {
		AddLabel("Command Line", container, true);
		         
		tbCommand = new Entry();
		tbCommand.Name = "CMD";
		tbCommand.Text = ss.CommandLineOptions;
		tbCommand.Sensitive = false;
		
		container.Add(tbCommand);
		DontExpand(container, tbCommand);
	}
	
	private void CreateFileBrowser(XmlNode n, Container container) {
		HBox hb = new HBox(false, 6);
		                
		AddLabel(Value2String(n, "_label"), hb, true);
		         
		Entry tb = new Entry();
		tb.Name = Value2String(n, "id") + UniqueID();
		tb.Text = GetCurrentValue(Value2String(n, "arg"), "");
		ssSettings.Add("txt" + tb.Name, new settingsData(Value2String(n, "arg")));
		hb.Add(tb);
		
		Button btn = new Button("gtk-open");
		btn.Name = "b" + tb.Name;
		btn.Clicked += ShowFileOpenDialog;
		hb.Add(btn);
		
		container.Add(hb);
		
		DontExpand(container, hb);
	}
		
	private void CreateCombobox(XmlNode n, Container container) {
		ListStore cList = new ListStore(typeof(string), typeof(string));
		ComboBox cb = new ComboBox(cList);
		cb.Name = Value2String(n, "id") + UniqueID();
		
		CellRendererText cell = new CellRendererText();
		cb.PackStart(cell, false);
		cb.AddAttribute(cell, "text", 0);		
		
		string arg;
		string id;
		int itemIndex = 0;
		int selIndex = -1;
		int specialIndex = -1;
		foreach(XmlNode sn in n.ChildNodes) {
			id = Value2String(sn, "id");
			cList.AppendValues(Value2String(sn, "_label"), id);

			if(id == "none" || !AttrExists(sn, "arg-set")) specialIndex = itemIndex;

			arg = Value2String(sn, "arg-set");
			ssSettings.Add("cb" + cb.Name + id + itemIndex.ToString(), new settingsData(arg));
			//if(ss.CurrentValues.ContainsKey(arg)) selIndex = itemIndex;
			if(ss.HasParam(arg)) selIndex = itemIndex;
			
			itemIndex++;
		}
		if(selIndex == -1) {
			if(specialIndex != -1) cb.Active = specialIndex;
		} else {
			cb.Active = selIndex;
		}
		container.Add(cb);
		
		DontExpand(container, cb);
	}
	
	private void CreateSpinButton(XmlNode n, Container container) {
		HBox hc = new HBox(false, 6);

		AddLabel(Value2String(n, "_label"), hc, true);

		SpinButton sb = new SpinButton(Value2Int(n, "low"), Value2Int(n, "high"), 1);
		sb.Name = Value2String(n, "id") + UniqueID();
		sb.Value = GetCurrentValue(Value2String(n, "arg"), Value2Int(n, "default"));
		ssSettings.Add("sb" + sb.Name, new settingsData(
		                                         Value2String(n, "arg"),
		                                         Value2String(n, "low").Contains("."),
		                                         (Value2String(n, "convert") == "invert"),
		                                         Value2Int(n, "low"),
		                                         Value2Int(n, "high")
		                                         )
		               );
		if(Value2String(n, "convert") == "invert") {
			sb.Value = Value2Int(n, "high") - (sb.Value - Value2Int(n, "low"));
		}
		hc.Add(sb);
		DontExpand(hc, sb);
			
		container.Add(hc);
		DontExpand(container, hc);
	}
	
	private void DontExpand(Container container, Widget w) {
		Box.BoxChild bc = (Box.BoxChild)container[w];
		bc.Expand = false;
		bc.Fill = false;		
	}
	
	private void DoExpand(Container container, Widget w) {
		Box.BoxChild bc = (Box.BoxChild)container[w];
		bc.Expand = true;
		bc.Fill = true;
	}
	
	private void CloseWindow(object sender, EventArgs e) {
		this.Hide();
	}

	private void LoadDefaults(object sender, EventArgs e) {
		Dialog dlg = new MessageDialog(this, 
		                               DialogFlags.DestroyWithParent, 
		                               MessageType.Info, 
		                               ButtonsType.Close, 
		                               "This feature has not been implemented yet"); 
		dlg.Run();
		dlg.Destroy();
	} 
	
	private void ShowFileOpenDialog(object sender, EventArgs e) {
		FileChooserDialog dlg = new FileChooserDialog("Select File", 
		                                   this, 
		                                   FileChooserAction.Open, 
		                                   "gtk-cancel", 
		                                   ResponseType.Cancel, 
		                                   "gtk-ok", 
		                                   ResponseType.Ok);
		
		Button btn = (Button)sender;
		string tbName = btn.Name.Substring(1);
		
		//I already know the name the of the textbox... why do I need
		// to iterate through all the items in the container to find it???
		Container c = (Container)((Button)sender).Parent;
		Entry tb = null;
		foreach(Widget w in c.Children) {
			if(w.GetType() == typeof(Entry)) {
				tb = (Entry)w;
				if(tb.Name == tbName) break;
			}
		}
		
		if(dlg.Run() == (int)ResponseType.Ok) {
			tb.Text = dlg.Filename;
		}
		
		dlg.Destroy();
	}
	
	private string UniqueID() {
		long i = 1;
 		foreach (byte b in Guid.NewGuid().ToByteArray()) {
			i *= ((int)b + 1);
		}
 		return string.Format("{0:x}", i - DateTime.Now.Ticks);
	}
	
	private void ShowDesc(object sender, EventArgs e) {
		Dialog dlg = new MessageDialog(this, 
		                               DialogFlags.DestroyWithParent, 
		                               MessageType.Info, 
		                               ButtonsType.Close, 
		                               ss.Description);
		dlg.Run();
		dlg.Destroy();
	}
		
	private float Value2Int(XmlNode node, string attribute) {	
		if(AttrExists(node, attribute)) {
			float val = 0F;
			float.TryParse(node.Attributes.GetNamedItem(attribute).Value, out val);
			return val;
		} else {
			return 0F;
		}
	}
	
	private string Value2String(XmlNode node, string attribute) {
		if(AttrExists(node, attribute)) {			
			return node.Attributes.GetNamedItem(attribute).Value;
		} else {
			return "";
		}
	}
	
	// I had to create this function because, for some reason, exceptions crash
	//  mono under openSUSE... go figure...
	private bool AttrExists(XmlNode node, string attribute) {
		foreach(XmlAttribute attr in node.Attributes) {
			if(attr.Name == attribute) return true;
		}
		return false;
	}
	
	/*
	private bool Value2Bool(XmlNode node, string attribute) {
		bool val = false;
		bool.TryParse(node.Attributes.GetNamedItem(attribute).Value, out val);
		return val;
	}
	*/
	
	private float GetCurrentValue(string key, float defval) {
		string sval;		
		if(ss.CurrentValues.TryGetValue(key.Replace(" %", ""), out sval)) {
			float val;
			float.TryParse(sval, out val);
			return val;
		}
		return defval;
	}
	
	private string GetCurrentValue(string key, string defval) {
		string sval = "";
		if(ss.CurrentValues.TryGetValue(key, out sval)) {
			return sval;
		}
		return defval; 		                    
	}
	
	private bool GetCurrentValue(string key, bool defval) {
		if(ss.CurrentValues.ContainsKey(key)) {
			return true;
		}
		return defval; 		                    
	}
}