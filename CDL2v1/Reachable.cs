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

      public class CrossReferenceSet : Dictionary<CDL2Object,Set<NamedElement>> {
         public void Add(CDL2Object obj,NamedElement referrencer) {
            if (!ContainsKey(obj)) base.Add(obj,[]);
            this[obj].Add(referrencer);
         }
      }
      public class CrossReferencedObjectSet : Set<CDL2Object> {
         public readonly CrossReferenceSet CrossReferences = []; // Maps each object to the set of objects that reference it. Only includes objects reachable from the entry point.
         public bool Add(CDL2Object obj,NamedElement referrer) {
            CrossReferences.Add(obj,referrer); // Always add a reference
            return base.Add(obj);
         }
         public static new bool Add(CDL2Object obj) => throw new InvalidOperationException("Use Add(CDL2Object obj, NamedElement referrer) instead to keep track of cross references.");

         public Set<NamedElement> Referrers(CDL2Object obj) => CrossReferences.TryGetValue(obj,out Set<NamedElement>? referrers) ? referrers : [];
         public new bool Remove(CDL2Object obj) {
            CrossReferences.Remove(obj);
            return base.Remove(obj);
         }
         public new void Clear() {
            base.Clear();
            CrossReferences.Clear();
         }
      }

      public CrossReferencedObjectSet Objects {
         get {
            Debug.Assert(collecting || collected,"Must call CollectReachableObjects(program/module)  before accessing Objects");
            return field;
         }
         set {
            if (value is not null && value.Count != 0) throw new ArgumentException("CrossReferencedObjectSets can only be set to a null or an empty set");
            field.Clear();
         }
      } = [];

      public Set<CDL2Object> AllObjects = []; // All objects in the program/module, including those not reachable from the entry point.

      public IDDictionary<int> ProcedureCalls = []; // The number of times a procedure is called

      public CrossReferenceSet XRefs => Objects.CrossReferences;

      // Used to track the variables that are read in the program. Write references are in <see cref="ReferencedObjects."/>.
      public Set<ITrackedVar> ReadVars { get; private set; } = [];
      public Set<ITrackedVar> AmbigousVars { get; private set; } = [];

      // The set of procedures that are recursive, i.e., that call themselves directly or indirectly.
      public Set<Procedure> RecursiveProcedures = []; 

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
         LogObjectCount(this,$"in {moduleCount.Plural("module")}",allObjects:true);
      }

      /// <summary>
      /// Collects all objects reachable from the given program.
      /// Clears previous collection results first.
      /// </summary>
      /// <param name="prog"></param>
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
         foreach (Procedure proc in Objects.OfType<Procedure>()) {
            if (HasRecursion(proc.group,proc.Id,[])) RecursiveProcedures.Add(proc);
         }

         collecting = false;
         collected = true;
         LogObjectCount(this,$"reachable from {prog}");
      }

      /// <summary>
      /// Return true if the given group contains a call to the given procedure, directly or indirectly. This is used to determine
      /// if a procedure is recursive.
      /// </summary>
      /// <param name="group"></param>
      /// <param name="procId"></param>
      /// <returns></returns>
      private static bool HasRecursion(Group group,ID procId,Set<ID> seen) {
         foreach (Alternative alt in group.Alternatives) {
            foreach (Call call in alt.Calls) {
               if (IsRecursiveCall(call,procId,seen)) return true;
            }
            if (alt.LastCall.type == LCT.Standard) return IsRecursiveCall(alt.LastCall.call!,procId,seen);
            if (alt.LastCall.type == LCT.Group) return HasRecursion(alt.LastCall.group!,procId,seen);
         }
         return false;

         static bool IsRecursiveCall(Call call,ID procId,Set<ID> seen) {
            if (call.Id == procId) return true;
            if (seen.Contains(call.Id)) return false;
            seen.Add(call.Id);
            Algorithm called = call.Called!;
            return called is Procedure proc && HasRecursion(proc.group,procId,seen);
         }
      }

      public static void LogObjectCount(Reachable reachable,string sort,Action<string>? logger = null,int logLevel = 1,bool unused = false,bool allObjects=false) {
         Set<CDL2Object> objects = allObjects ? reachable.AllObjects : reachable.Objects;
         logger ??= str => Logger.Log(logLevel,str);
         string CountObjects<T>(bool noComma = false) => objects.Where(obj => obj.GetType() == typeof(T)).Count().Plural(typeof(T).Name.Capitalize,noComma ? null : ",",countWidth:1);

         logger($"{objects.Count.Plural("object")}{(unused ? $" ({reachable.AllObjects.Count - reachable.Objects.Count} unused)" : "")} {sort}...");
         logger($"   {CountObjects<Const>()} {CountObjects<Var>()} {CountObjects<LIST>()} {CountObjects<Macro>()} {CountObjects<Procedure>(noComma: true)}.");
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
            if (section.LudeProcs[ludeType] is Guid guid && guid.ToNamedElement<Procedure>() is Procedure proc) {
               if (Objects.Add(proc!,section)) CollectReachableObjects(proc.group);
            } else {
               throw new NotImplementedException($"CollectReachableObjects: Could not find lude {section.Ludes[ludeType][0]} in {section}");
            }
         } else {
            // The section was mentioned in a Module lude, but it has no lude.
         }
      }
      private void CollectReachableObjects(Group group) {
         // Collect all the objects reachable from this group. If the alternative is governed by positive conditional compilation, then subsequent alternatives should not be processed.
         foreach (Alternative alt in group.Alternatives) if (CollectReachableObjects(alt)) return;
      }

      /// <summary>
      /// Collect all the objects reachable from this alternative.
      /// </summary>
      /// <param name="alt"></param>
      /// <returns>true if this alternative was governed by positive conditional compilation, i.e., that subsequent alternatives should <b>not</b> be processed.</returns>
      private bool CollectReachableObjects(Alternative alt) {
         if (alt.IsConditionalCompilationOff) return false;
         // Conditinal compilation is only suported for the first call of an alternative
         IEnumerable<Call> calls = alt.IsConditionalCompilationOn ? alt.Calls.Skip(1) : alt.Calls;
         foreach (Call call in calls) CollectReachableObjects(call); 
         switch (alt.LastCall.type) {
            case LCT.Standard:
               if (alt.LastCall.call is not null) CollectReachableObjects(alt.LastCall.call);
               break;
            case LCT.Group:
               if (alt.LastCall.group is not null) CollectReachableObjects(alt.LastCall.group);
               break;
         }
         return alt.IsConditionalCompilationOn;
      }

      /// <summary>
      /// Return false if the rest of the alternative containing the call is to be ignored.
      /// </summary>
      /// <param name="call"></param>
      /// <returns></returns>
      private void CollectReachableObjects(Call call) {
         Debug.Assert(!call.IsConditionalCompilationOff && !call.IsConditionalCompilationOn,
            "CollectReachableObjects(Call) should not get a conditional compilation call");
         if (call.Called is not null) {
            Algorithm called = call.Called;
            if (called is ImportedAlgorithm importedAlg) {
               CDL2Object? resolved = called.Section!.GetResolvedObject(importedAlg.Id);
               if (resolved != null && resolved is Algorithm alg) called = alg;
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
               if (i > called.Affixes.Count-1) break; // The argument mismatch must have been signalled earlier
               Affix affix = called.Affixes[i++];
               switch (arg) {
                  case Const c:
                     CollectReachableObjects(c,call.ContainingProc);
                     break;
                  case Var v:
                     Objects.Add(v,call.ContainingProc);
                     if (affix.IsInput) ReadVars.Add(v);
                     break;
                  case ID id:
                     if (call.Called.Section!.TryGetDeclaration(id,out CDL2Object? obj)) {
                        if (obj is Const c) {
                           CollectReachableObjects(c,call.ContainingProc);
                        } else if (obj is ImportedConst ic1 && ic1.Module!.resolvedImports.TryGetValue(ic1.Id,out IImportable? elem1) && elem1 is Const rc1) {
                           CollectReachableObjects(rc1,call.ContainingProc);
                        } else if (obj is Var v) {
                           Objects.Add(v,call.ContainingProc);
                        }
                     }
                     break;
               }
            }
            if (Objects.Add(called,call.ContainingProc)) {
               if (called is Macro macro) {
                  CollectReachableObjects(macro);
               } else if (called is Procedure proc) {
                  CollectReachableObjects(proc.group);
               }
            }
         }
      }
      private void CollectReachableObjects(Const constant,NamedElement referer) {
         if (constant is ImportedConst) constant = (constant.Module!.resolvedImports[constant.Id] as Const)!;
         if (Objects.Add(constant,referer)) {
            foreach (IElement elem in constant.elements) {
               switch (elem) {
                  case ID id:
                     if (constant.ParentElement<Section>()!.TryGetDeclaration(id,out CDL2Object? obj)) {
                        if (obj is Const c) CollectReachableObjects(c,referer);
                     } else {
                        throw new NotImplementedException($"CollectReachableObjects: Unresolved reference to {id}");
                     }
                     break;
               }
            }
         }
      }
      private void CollectReachableObjects(Macro macro) {
         foreach (IElement element in macro.Elements) {
            switch (element) {
               case Affix:
               case Local:
                  break;
               case ID id:
                  if (macro.Section!.TryGetDeclaration(id,out CDL2Object? obj)) {
                     switch (obj) {
                        case Const c:
                           CollectReachableObjects(c,macro);
                           break;
                        case Var v:
                           Objects.Add(v,macro);
                           break;
                        case LIST l:
                           CollectReachableObjects(macro,l);
                           break;
                     }
                  }
                  break;
               case Const c:
                  CollectReachableObjects(c,macro);
                  break;
               case Var v:
                  Objects.Add(v,macro);
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
         if (Objects.Add(list,macro)) {
            if (macro.Section!.TryGetDeclaration(list.lwb,out Const? lwb)) CollectReachableObjects(lwb!,list);
            if (macro.Section.TryGetDeclaration(list.upb,out Const? upb)) CollectReachableObjects(upb!,list);
         }
      }
   }
}

