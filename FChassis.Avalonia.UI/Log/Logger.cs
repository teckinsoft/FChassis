using Avalonia.Controls;
using System;
using System.Collections.Generic;
using ADocuments = Avalonia.Controls.Documents;

using LogInlines = System.Collections.Generic.List<object>;

namespace FChassis;
/// <summary>
/// Logger is utility class to add log entry and display 
/// Usage. Logger is singleton class.
/// Logger instance is accessed with Instance() function.
/// Before use Logger set TextBlock control with SetControl() function.
/// Use Add(type, message) is function to add log entry.
/// Simplified Add() function usage types
///   1. Add("Message-Text")
///   2. Add(<Logger.Type>, "Message-Text")
///   3. Add(Logger.Type.Blank)
///   4. Add(Logger.Type.Line)
/// </summary>
public class Logger {
   private Logger () { }

   #region Method -------------------------------------------------------------
   /// <summary>
   /// This function return singleton instance of Logger class
   /// </summary>
   public static Logger Instance {
      get { 
         if (Logger.logger == null)
            Logger.logger = new Logger ();

         return Logger.logger;
      }
   }

   /// <summary>
   /// Function to set TextBlock control. This is one time call function 
   /// </summary>
   /// <param name="textBlock">Instance of TextBlock control to which Logger add log entries</param>
   public static void SetControl(TextBlock textBlock) {
      if (Logger.logger == null)
         Logger.logger = new Logger ();

      if(Logger.logger.textBlock == null)
         Logger.logger.textBlock = textBlock;

      Logger.logger?.textBlock.Inlines!.Clear ();
   }   

   /// <summary>
   /// Function to add log entry.
   /// </summary>
   /// <param name="type">Log Entry Type: Normal, Info, Warning, Eroor, Blank, Line</param>
   /// <param name="message">Message Text to add as log entry</param>
   public void Add (LogType type = LogType.Normal, string message = "") {
      if (this.textBlock == null)
         return;

      // Remove first Line if count >= LineMax
      if (this.paragraphs.Count >= Logger.LineMax) {
         LogInlines firstLine = this.paragraphs[0];
         foreach(var inline in firstLine)
            this.textBlock.Inlines!.Remove ((ADocuments.Run)inline);

         this.paragraphs.Remove (firstLine);
      }

      // Add new entry
      string _type = " Normal";
      Avalonia.Media.IBrush color = Avalonia.Media.Brushes.Black;
      switch(type) { 
         case LogType.Info:
            _type = "   Info";
            color = Avalonia.Media.Brushes.LightSeaGreen;
            break;

         case LogType.Warning:
            _type = "Warning";
            color = Avalonia.Media.Brushes.Goldenrod; // For Yellow
            break;

         case LogType.Error:
            _type = "  Error";
            color = Avalonia.Media.Brushes.Red;
            break;
      };

      LogInlines logInlines = new LogInlines ();
      ADocuments.Run run = new ADocuments.Run () {
         FontFamily = new Avalonia.Media.FontFamily (Logger.FontFamily)
      };

      if (type != LogType.Blank)
         run.Text = $"[{DateTime.Now}]";

      switch (type) {
         case LogType.Blank:
            run.Text += "\n";
            addRun ();
            break;

         case LogType.Line:
            run.Text += "-------------------------------------------------------------------------------\n";
            break;

         default:
            addRun ();

            run = new ADocuments.Run () {
               Text = _type,
               Foreground = color,
               FontFamily = new Avalonia.Media.FontFamily (Logger.FontFamily),
               FontWeight = Avalonia.Media.FontWeight.ExtraBold
            };
            addRun ();

            run = new ADocuments.Run () {
               Text = $": {message}\n",
               FontFamily = new Avalonia.Media.FontFamily (Logger.FontFamily),
            };
            addRun ();
            break;
         }

      if (run != null) {
         this.textBlock.Inlines!.Add (run);
         logInlines.Add (run);
      }

      if(logInlines.Count > 0) 
         this.paragraphs.Add (logInlines);

      void addRun() {
         this.textBlock.Inlines!.Add (run);
         logInlines.Add (run);
         run = null!;
      }
   }

   public void Add (string message) => Add (LogType.Normal, message);
   #endregion Method 

   #region Nested class ----------------------------------------------------
   /// <summary>
   ///  Log entry Types enumeration
   /// </summary>
   public enum LogType {
      Normal,
      Info,
      Warning,
      Error,
      Blank,
      Line,
   }
   #endregion Nested class 

   // Private Data ---------------------------------------------------------
   static Logger? logger;                    // Class Singleton Instance

   const string FontFamily = "Courier";      // Equal spaced character font name
   const int LineMax = 50;                   // Line Max is Log entries count to retain in display

   TextBlock? textBlock;                     // TextBlock Instance to which Log entries managed
   List<LogInlines> paragraphs = new List<LogInlines> ();
}
