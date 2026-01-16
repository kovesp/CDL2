// <auto-gen>
//=======================================================================
// <copyright file="Reachable.cs" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>2025-04-13</creation-date>
// 
// <summary>
//   Collects the objects in the pare tree and those that are reachable from a given program.
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

using System.Collections.Immutable;
using System.Diagnostics;

namespace CDL2v1 {
   public class Reachable {
      public void DumpReachableObjects(Program prog) {
         Debug.WriteLine($"Reachable Objects for {prog}:");
         foreach (string objectName in Objects.Select(obj => ((NamedElement)obj).FQDN()).ToImmutableSortedSet()) {
            Debug.WriteLine($"   {objectName}");
         }
         Debug.WriteLine("End of reachable objects.");
      }
      private bool collecting = false;
      private bool collected = false;
      public Set<CDL2Object> Objects {
         get {
            Debug.Assert(collecting || collected,"Must call CollectReachableObjects(program/module)  before accessing Objects");
            return field;
         }
         private set;
      } = [];
      public Set<CDL2Object> AllObjects = []; // All objects in the program/module, including those not reachable from the entry point.

      public IDDictionary<int> ProcedureCalls = []; // The number of times a procedure is called

      // Used to track the variables that are read in the program. Write references are in <see cref="ReferencedObjects."/>.
      public Set<ITrackedVar> ReadVars { get; private set; } = [];
      public Set<ITrackedVar> AmbigousVars { get; private set; } = [];

      public void CollectAllObjects(Program? program) {
         // Collect all objects in DB
         AllObjects = (Set<CDL2Object>)Database.NamedElementsOfType<CDL2Object>(elem => !elem.IsImported,e => e.ToSet);
         int moduleCount = Database.Instance.Modules.Count;
         if (program is not null) {
            // Keep only objects that are defined in one of the modules of the program
            IEnumerable<Module> programModules = program.Modules;
            moduleCount = program.Parts.Count;
            AllObjects = AllObjects.Where(obj => !obj.IsImported && programModules.Contains(obj.Module)).ToSet;
         }
         LogObjectCount(AllObjects,$"in {moduleCount.Plural("module")}");
      }

      public void CollectReachableObjects(Program prog) {
         collected = false;
         collecting = true;
         Objects = [];
         ReadVars = [];
         AmbigousVars = [];
         ProcedureCalls = [];

         foreach (RW ludeType in Container.LudeTypes) {
            foreach (ID id in prog.Ludes[ludeType]) {
               Module? module = Database.Instance.ModuleByName(id);
               if (module is not null) {
                  CollectReachableObjects(ludeType,module);
               }
            }
         }
         collecting = false;
         collected = true;
         LogObjectCount(Objects,$"reachable from {prog}");
      }

      public static void LogObjectCount(Set<CDL2Object> objects,string sort,Action<string>? logger = null,int logLevel=1) {
         logger ??= str => Logger.Log(logLevel,str);
         string CountObjects(Type type,Set<CDL2Object> objects,bool noComma = false) => objects.Where(obj => obj.GetType() == type).Count().Plural(type.Name,noComma ? null : ",");
         logger($"{objects.Count.Plural("object")} {sort} ...");
         logger($"   {CountObjects(typeof(Const),objects)} {CountObjects(typeof(Var),objects)} {CountObjects(typeof(LIST),objects)} {CountObjects(typeof(Macro),objects)} {CountObjects(typeof(Procedure),objects,noComma: true)}.");
      }

      public void CollectReachableObjects(Module module) => throw new NotImplementedException($"CollectReachableObjects: Not yet implemented for modules.");
      private void CollectReachableObjects(RW LudeType,Module module) {
         foreach (Section? section in module.Ludes[LudeType].Select(id => module.SectionById(id))) {
            if (section is not null) CollectReachableObjects(LudeType,section);
         }
      }
      private void CollectReachableObjects(RW ludeType,Section section) {
         // Section ludes contain the single entry of a synthetic procedure that is the lude.
         // So we need to collect all the objects in the section that are reachable from this lude.
         List<ID> ds = section.Ludes[ludeType];
         if (ds.Count == 1) {
            if (section.LudeProcs[ludeType] is Guid guid && guid.ToCDL2Object<Procedure>() is Procedure proc) {
               if (Objects.Add(proc!)) CollectReachableObjects(proc.group);
            } else {
               throw new NotImplementedException($"CollectReachableObjects: Could not find lude {section.Ludes[ludeType][0]} in {section}");
            }
         } else {
            // The section was mentioned in a Module lude, but it has no lude.
         }
      }
      private void CollectReachableObjects(Group proc) {
         // Collect all the objects reachable from this group.
         foreach (Alternative alt in proc.Alternatives) if (CollectReachableObjects(alt)) break;
      }

      /// <summary>
      /// Collect all the objects reachable from this alternative.
      /// </summary>
      /// <param name="alt"></param>
      /// <returns>true if this alternative was governed by positive conditional compilation, i.e., that subsequent alternatives should <b>not</b> be processed.</returns>
      private bool CollectReachableObjects(Alternative alt) {
         foreach (Call call in alt.calls) {
            if (!CollectReachableObjects(call)) return true; // Skip the rest of the alternative.
         }
         switch (alt.lastCall.type) {
            case LCT.Standard:
               if (alt.lastCall.call is not null) CollectReachableObjects(alt.lastCall.call);
               break;
            case LCT.Group:
               if (alt.lastCall.group is not null) CollectReachableObjects(alt.lastCall.group);
               break;
         }
         return alt.IsConditionalCompilationOn;
      }

      /// <summary>
      /// Return false if the rest of the alternative containing the call is to be ignored.
      /// </summary>
      /// <param name="call"></param>
      /// <returns></returns>
      private bool CollectReachableObjects(Call call) {
         if (call.Called is not null) {
            Algorithm called = call.Called;
            if (called is ImportedAlgorithm importedAlg) {
               CDL2Object? resolved = called.Section!.GetResolvedObject(importedAlg.Id);
               if (resolved != null && resolved is Algorithm alg) called = alg;
            }
            if (called.IsConditionalCompilation()) {
               Objects.Add(called);
               return called.IsConditionalCompilationOn;   // If false, skip the rest of the alternative.
            }

            if (called is Procedure calledProc) {
               if (ProcedureCalls.TryGetValue(calledProc.Id,out int count)) {
                  ProcedureCalls[calledProc.Id] = count + 1;
               } else {
                  ProcedureCalls[calledProc.Id] = 1;
               }
            }

            // Collect objects referenced in actual args
            int i = 0;
            foreach (IActualArg arg in call.Args) {
               Affix affix = called.Affixes[i++];
               switch (arg) {
                  case Const c:
                     CollectReachableObjects(c);
                     break;
                  case Var v:
                     Objects.Add(v);
                     if (affix.IsInput) ReadVars.Add(v);
                     break;
                  case ID id:
                     if (call.Called.Section!.TryGetDeclaration(id,out CDL2Object? obj)) {
                        if (obj is Const c) {
                           CollectReachableObjects(c);
                        } else if (obj is ImportedConst ic1 && ic1.Module!.resolvedImports.TryGetValue(ic1.Id,out IImportable? elem1) && elem1 is Const rc1) {
                           CollectReachableObjects(rc1);
                        } else if (obj is Var v) {
                           Objects.Add(v);
                        }
                     } else {
                        throw new NotImplementedException($"CollectReachableObjects: Unresolved reference to {id}");
                     }
                     break;
               }
            }
            if (Objects.Add(called)) {
               if (called is Macro macro) {
                  CollectReachableObjects(macro);
               } else if (called is Procedure proc) {
                  CollectReachableObjects(proc.group);
               }
            }
         }
         return true;
      }
      private void CollectReachableObjects(Const constant) {
         if (constant is ImportedConst) constant = (constant.Module!.resolvedImports[constant.Id] as Const)!;
         if (Objects.Add(constant)) {
            foreach (IElement elem in constant.elements) {
               switch (elem) {
                  case ID id:
                     if (constant.ParentElement<Section>()!.TryGetDeclaration(id,out CDL2Object? obj)) {
                        if (obj is Const c) CollectReachableObjects(c);
                     } else {
                        throw new NotImplementedException($"CollectReachableObjects: Unresolved reference to {id}");
                     }
                     break;
               }
            }
         }
      }
      private void CollectReachableObjects(Macro macro) {
         foreach (IElement element in macro.elements) {
            switch (element) {
               case Affix:
               case Local:
                  break;
               case ID id:
                  if (macro.Section!.TryGetDeclaration(id,out CDL2Object? obj)) {
                     switch (obj) {
                        case Const c:
                           CollectReachableObjects(c);
                           break;
                        case Var v:
                           Objects.Add(v);
                           break;
                        case LIST l:
                           CollectReachableObjects(macro,l);
                           break;
                     }
                  }
                  break;
               case Const c:
                  CollectReachableObjects(c);
                  break;
               case Var v:
                  Objects.Add(v);
                  if (macro.HasNoEffect) {
                     // Assume the variable is read. Because (1) it can't be written, otherwise it would not meet the macro contract and (2) it is referenced so it must be read.
                     // OTOH, with ACTIONs/PREDICATEs we can't tell.
                     ReadVars.Add(v);
                     AmbigousVars.Remove(v);
                  } else if (!ReadVars.Contains(v)) {
                     AmbigousVars.Add(v);
                  }
                  break;
               case LIST l:
                  CollectReachableObjects(macro,l);
                  break;
            }
         }
      }
      private void CollectReachableObjects(Macro macro,LIST list) {
         if (Objects.Add(list)) {
            if (macro.Section!.TryGetDeclaration(list.lwb,out Const? lwb)) CollectReachableObjects(lwb!);
            if (macro.Section.TryGetDeclaration(list.upb,out Const? upb)) CollectReachableObjects(upb!);
         }
      }
   }
}

