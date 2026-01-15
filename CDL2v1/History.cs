// <auto-gen>
//=======================================================================
// <copyright file="History.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-06-10</creation-date>
// 
// <summary>
//   Implements the GUI logic (code-behind) for the CommandWindow.
// </summary>
// <attribution>
//   This file is part of the clean room reimplementation of the
//      CDL2 Compiler
//      CDL2 Laboratory
//      CDL2 Target Code Generators
//
//    Based on original work on CDL and CDL2 led by C. H. A. Koster
//    and the CDL2 team at the Universities of Berlin, Germany and
//    Nijmegen, The Netherlands.
//
//    The CDL2 Laboratory was the work of Epsilon GmbH, Berlin.
//    H. M. Stahl, H. Feuerhahn, JP. Dehotay, B. Böhringer
//    (and others I don't remember ... sorry).
//
//    This project is not affiliated with the original CDL2 project.
// </attribution>
//=======================================================================
// </auto-gen>

namespace CDL2v1 {
   /// <summary>
   /// </summary>
   /// Command history manager
   public class History {
      private readonly List<string> _history = [];
      private int _currentIndex = -1;

      /// <summary>
      /// Gets or sets the collection of command strings maintained in the command history.
      /// </summary>
      /// <remarks>When setting this property, the existing command history is replaced with the
      /// provided collection, and the current index is reset to the end of the new history. The number of commands
      /// returned when getting this property is limited by the configured command history size.</remarks>
      public IEnumerable<string> Commands {
         get => _history.TakeLast(Settings.SettingValue<int>("CommandHistorySize"));
         set {
            _history.Clear();
            _history.AddRange(value);
            _currentIndex = _history.Count;
         }
      }

      public void Add(string command) {
         _history.Add(command);
         _currentIndex = _history.Count;
      }

      public string? Previous() {
         if (_history.Count == 0 || _currentIndex == 0) return null;

         _currentIndex = Math.Max(0,_currentIndex - 1);
         return _currentIndex < _history.Count ? _history[_currentIndex] : "";
      }

      public string? Next() {
         if (_history.Count == 0 || _currentIndex >= _history.Count) return null;

         _currentIndex = Math.Min(_history.Count,_currentIndex + 1);
         return _currentIndex < _history.Count ? _history[_currentIndex] : "";
      }
   }
}

