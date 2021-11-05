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
using System.IO;
using System.Collections.Generic;
using Gnome;

public class ScreenSaver {
	private XmlDocument mXML;
	private XmlNode ssNode;
	private string mFileName;
	private string mDesktopFileName;
	private string mSSName;
	private string mSSComment;
	private Dictionary<string, string> mCurrentValues = new Dictionary<string, string>();
	private DesktopItem desktopFile;
	private bool mIsValid = true;
	private bool mIsReadOnly = false;
	private bool mHasChanged = false;
	private bool mHasConfig = false;
	private Exception error = null;
	private bool mSelected = false;
			
	public ScreenSaver(FileInfo file) {
		try {
			mSSName = file.Name.Split(new string[] {"."}, StringSplitOptions.None)[0];
			mFileName = MainClass.SS_BIN_PATH + mSSName;
			mDesktopFileName = MainClass.SS_THEME_PATH + mSSName + ".desktop";
			
			mXML = new XmlDocument();
			mXML.Load(file.FullName);
			
			XmlNodeList nl = mXML.GetElementsByTagName("screensaver");
			if(nl.Count == 1) {
				ssNode = nl[0];
				GetCurrentValues();
			}
		} catch(Exception e) {
			error = e;
			mIsValid = false;
		}
		
		if(mIsValid) mIsValid = File.Exists(BinaryFullFileName);
	}
	
	public ScreenSaver(DesktopItem currentDesktop, TextReader xmlFile) {
		try {
			desktopFile = currentDesktop.Copy();
			mSSName = desktopFile.GetString("Name");
			mSSComment = desktopFile.GetString("Comment");
			List<string> cmdLine = new List<string> (desktopFile.GetString("Exec").Split((char[])null,
			                                                                              StringSplitOptions.RemoveEmptyEntries));
			CommandLine = cmdLine[0];
			cmdLine.RemoveAt(0);
			foreach (string option in cmdLine)
				if(option != "-root")
					CommandLine += " " + option;
			
			if(xmlFile != null) {
				mXML = new XmlDocument();
				mXML.Load(xmlFile);
			
				XmlNodeList nl = mXML.GetElementsByTagName("screensaver");
				if(nl.Count == 1) {
					ssNode = nl[0];
					UpdateCurrentValues();
				}
				mHasConfig = true;
			}
		} catch(Exception e) {
			error = e;
			mIsValid = false;
		}
		
		if(mIsValid) mIsValid = File.Exists(BinaryFullFileName);
	}
	
	public static string GetConfigFile(DesktopItem desktopFile)
	{
		string fileName = GetFileName(desktopFile.GetString("Exec").Split(' ')[0]);
		return Path.Combine(MainClass.SS_XML_PATH, fileName + ".xml");
	}
	
	~ScreenSaver() {
		if(mHasChanged) ApplyCommandLine();
	}
	
	public bool IsValid {
		get {return mIsValid;}
	}
	
	public bool HasConfig {
		get {return mHasConfig;}
	}
	
	public bool Selected {
		get { return mSelected; }
		set { mSelected = value; }
	}
	
	public Exception Error {
		get {return error;}
	}

	public bool IsReadOnly {
		get {
			if(mHasConfig) {
				return mIsReadOnly;
				/*
				//This is obviously wrong but, for some reaon, querying the "IsReadOnly"
				//property of FileInfo and UnixFileInfo always return true...
				return !UnixUserInfo.GetRealUser().UserName.StartsWith("root");
				*/
			} else {
				return false;
			}
		}
	}
	
	public string Title {
		get {
			if(mHasConfig) {
				XmlNode a = ssNode.Attributes.GetNamedItem("_label");
				if(a != null) return a.Value;
			} 
			return mSSName;
		}
	}
	
	public string Description {
		get {
			if(mHasConfig) {
				foreach(XmlNode n in ssNode.ChildNodes) {
					if(n.Name == "_description") {
						return n.InnerText;
					}
				}
			}
			return mSSComment;
		}
	}
	
	public string Name {
		get {return desktopFile.GetString("Name");}
	}
	
	public string FileName {
		get {return mFileName;}
	}
	
	public XmlNodeList UIData {
		get {return ssNode.ChildNodes;}
	}
	
	public Dictionary<string, string> CurrentValues {
		get {return mCurrentValues;}
	}
	
	public string CommandLine {
		get {return desktopFile.GetString("Exec");}
		set {desktopFile.SetString("Exec", value);}
	}
	
	public string CommandLineOptions {
		get {
			string [] options = CommandLine.Split (' ');
			return string.Join (" ", options, 1, options.Length - 1);
		}
	}
	
	public bool HasChanged {
		get {return mHasChanged;}
	}
	
	public bool Configurable {
		get {return mXML != null;}
	}
	
	public string BinaryFullFileName {
		get { return Path.Combine (MainClass.SS_BIN_PATH, 
			                       this.BinaryFileName);
		}
	}
	
	public string BinaryFileName {
		get { return GetFileName(CommandLine.Split(' ')[0]); }
	}
	
	public string GConfName {
		get { return "screensavers-" + BinaryFileName; }
	}
	
	public bool HasParam(string param) {
		string arg = "";
		string val = "";
		if(param.Contains(" ")) {
			arg = MainClass.Split(param, " ")[0];
			val = MainClass.Split(param, " ")[1];
		} else {
			arg = MainClass.Split(param, " ")[0];
		}
		foreach(KeyValuePair<string, string> v in mCurrentValues) {
			if(v.Key == arg && v.Value == val) return true;
		}
		return false;
	}
	
	private void GetCurrentValues() {
		FileInfo file = new FileInfo(mDesktopFileName);
		if(!file.Exists) return;
		
		string[] lines = File.ReadAllLines(file.FullName);
		
		foreach(string line in lines) {
			if(line.Contains("Exec") && line.Contains(mSSName) && !line.Contains("TryExec")) {
				CommandLine = MainClass.Split(line, mSSName)[1].Replace("-root", "").Trim();
				UpdateCurrentValues();
				
				/*
				Console.WriteLine(mSSName);
				if(mSSName == "biof") {
					foreach(KeyValuePair<string, string> v in mCurrentValues) {
						Console.WriteLine(String.Format("'{0}' -> '{1}'", v.Key, v.Value));
					}
				}
				*/				
				        
				break;
			}
		}
	}
	
	private void UpdateCurrentValues() {
		mCurrentValues.Clear();
		string[] args = MainClass.Split(CommandLine.Trim(), " ");
		for(int i = 0; i < args.Length; i++) {
			if(mCurrentValues.ContainsKey(args[i])) {
				//Something went wrong here...
				RemoveFromCommandLine(args[i]);
				UpdateCurrentValues();
				break;
			} else {
				if(i == args.Length - 1) {
					//This is the last parameter so let's add it.
					mCurrentValues.Add(args[i], "");
				} else {
					if(args[i + 1].StartsWith("-") && !IsNumber(args[i + 1])) {
						//The next param starts with "-" and it's not a negative number
						//This means that it's another parameter, so let's add this one and continue.
						mCurrentValues.Add(args[i], "");
					} else {
						//The next param does not start with "-" or it does, but
						//we have determined that it's a number, so let's add them both.
						mCurrentValues.Add(args[i], args[i + 1]);
						i++;
					}
				}
			}
		}
	}
	
	public string ConfigFileName {
		get {
			return Path.GetFileName (desktopFile.Location);
		}
	}
	
	public void SaveConfig(string savePath) {
		DesktopItem saveFile = desktopFile.Copy();
		saveFile.SetString("Exec", CommandLine + " -root");
		try {
			saveFile.Save("file://" + Path.Combine(savePath, ConfigFileName), true);
		} catch(GLib.GException e) {
			throw new ApplicationException(String.Format("{0}\nUnable to save config to \"{1}\"",e.Message, savePath));
		}
	}
	
	public void ApplyCommandLine() {
		string userDesktopPath = UserDesktopPath;
		
		if(!Directory.Exists(userDesktopPath)) {
			Directory.CreateDirectory(userDesktopPath);
		}
		SaveConfig(userDesktopPath);
	}
	
	public static string UserDesktopPath {
		get {
			string userDesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			userDesktopPath = Path.Combine(userDesktopPath, "applications/screensavers");
			return userDesktopPath;
		}
	}
	
	public void RemoveFromCommandLine(string args) {
		if(args != "" && CommandLine.Contains(args)) {
			if(args.Contains(" ")) {
				//This solves a problem with some screensavers such as Sonar
				foreach(string arg in MainClass.Split(args, " ")) {	
					DoRemove(arg);
				}
			} else {
				DoRemove(args);
			}
			
			mHasChanged = true;
			UpdateCurrentValues();
		}
	}
	
	private void DoRemove(string arg) {
		string[] tokens = MainClass.Split(CommandLine, " ");
		for(int i = 0; i < tokens.Length; i++) {
			if(tokens[i] == arg) {
				tokens[i] = "";

				// Remove any parameters that this argument may contain
				bool isNum;
				bool hasHyp;
				for(int j = i + 1; j < tokens.Length; j++) {
					isNum = IsNumber(tokens[j]);
					hasHyp = tokens[j].StartsWith("-");
					
					if(isNum || !hasHyp) {
						//The parameter was a number isNum = true
						//Or, there's something after this param that is not a number
						// but is not a parameter either so let's get rid of it
						tokens[j] = "";
						break;
					} else {
						if(hasHyp) break;
					}
				}
				
				CommandLine = string.Join(" ", tokens).Trim();
				break;
			}
		}
	}
	
	public void AddToCommandLine(string arg) {
		CommandLine += " " + arg;
		do {
			CommandLine = CommandLine.Replace("  -", " -");
		} while(CommandLine.IndexOf("  -") != -1);
		
		mHasChanged = true;
		UpdateCurrentValues();
	}
						
	private bool IsNumber(string test) {
		double val;
		return double.TryParse(test, out val);
	}
	
	private static string GetFileName(string fullFileName) {
		if(fullFileName.Contains("/")) {
			string[] tokens = fullFileName.Split('/');
			return tokens[tokens.Length - 1];
		} else
			return fullFileName;
	}
}