using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
      public Set<CDL2Object> Objects {
         get {
            Debug.Assert(collecting || field.Count != 0, "Must call CollectReachebleObjects(program/module)  before accessing Objects");
            return field;
         }
         private set;
      } = [];
      public Set<CDL2Object> AllObjects = []; // All objects in the program/module, including those not reachable from the entry point.
      // Used to track the variables that are read in the program. Write references are in <see cref="ReferencedObjects."/>.
      public Set<ITrackedVar> ReadVars { get;  private set; } = [];
      public Set<ITrackedVar> AmbigousVars { get; private set; } = [];
      
      public void CollectAllObjects(Program program) {
         AllObjects = [];
         foreach (Module module in program.Modules) {
            foreach (Layer layer in module.Children.Cast<Layer>()) {
               foreach (Section section in layer.Children.Cast<Section>()) {
                  foreach (CDL2Object cdl2object in section.Declarations.Values) {
                     AllObjects.Add(cdl2object);
                  }
               }
            }
         }
         Logger.Log(0, $"Collected {AllObjects.Count} objects from {program}.");
      }

      public void CollectReachableObjects(Program prog) {
         Objects = [];
         ReadVars = [];
         AmbigousVars = [];
         collecting = true;
         foreach (RW ludeType in Container.LudeTypes) {
            foreach (ID id in prog.Ludes[ludeType]) {
               if (Database.Instance.Modules.TryGetValue(id, out Module? module)) {
                  CollectReachableObjects(ludeType, module);
               }
            }
         }
         collecting = false;
         string CountObjects(Type type) => Objects.Where(obj => obj.GetType() == type).Count().Plural(type.Name);
         Logger.Log(0, $"Collected {Objects.Count.Plural("object")} reachable from {prog} ...");
         Logger.Log(0, $"   {CountObjects(typeof(Const))}, {CountObjects(typeof(Var))}, {CountObjects(typeof(LIST))}, {CountObjects(typeof(Macro))}, {CountObjects(typeof(Procedure))}.");
      }
      public void CollectReachableObjects(Module module) => throw new NotImplementedException($"CollectReachableObjects: Not yet implemented for modules.");
      private void CollectReachableObjects(RW Ludetype, Module module) {
         foreach (Section? section in module.Ludes[Ludetype].Select(id => module.SectionById(id))) {
            if (section is not null) CollectReachableObjects(Ludetype, section);
         }
      }
      private void CollectReachableObjects(RW ludetype, Section section) {
         // SectionById ludes contain teh single entry of argAffix synthetic procedure that is the lude
         // So we need to collect all the objects in the section that are reachable from this lude.
         Debug.Assert(section.Ludes[ludetype].Count == 1, $"CollectReachableObjects: Expected single lude in {section}");
         if (section.Declarations.TryGetValue(section.Ludes[ludetype][0],out CDL2Object? obj) && obj is Procedure proc) {
            if (Objects.Add(proc!)) CollectReachableObjects(proc.group);
         } else {
            throw new NotImplementedException($"CollectReachableObjects: Could not find lude {section.Ludes[ludetype][0]} in {section}");
         }
      }
      private void CollectReachableObjects(Group proc) {
         // Collect all the objects reachable from this group.
         foreach (Alternative alt in proc.alternatives) CollectReachableObjects(alt);
      }

      private void CollectReachableObjects(Alternative alt) {
         foreach (Call call in alt.calls) {
            if (!CollectReachableObjects(call)) return; // Skip the rest of the alternative.
         }
         switch (alt.lastCall.type) {
            case LCT.Standard:
               if (alt.lastCall.call is not null) CollectReachableObjects(alt.lastCall.call);
               break;
            case LCT.Group:
               if (alt.lastCall.group is not null) CollectReachableObjects(alt.lastCall.group);
               break;
         }
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
               called = (called.Module!.resolvedImports[importedAlg.Id] as Algorithm)!;
            }
            if (called.IsConditionalCompilation()) {
               Objects.Add(called);
               return called.IsConditionalCompilationOn;   // If false, skip the rest of the alternative.
            }

            // Collect objects referrenced in actual args
            for (int i = 0 ; i < call.args.Count ; i++) {
               IActualArg arg = call.args[i];
               Affix affix = called.affixes[i];
               switch (arg) {
                  case Const c:
                     CollectReachableObjects(c);
                     break;
                  case Var v:
                     Objects.Add(v);
                     if (affix.IsInput) ReadVars.Add(v);
                     break;
                  case ID id:
                     if (call.Called.Section.TryGetDeclaration(id, out CDL2Object? obj)) {
                        if (obj is Const c) {
                           CollectReachableObjects(c);
                        } else if (obj is ImportedConst ic1 && ic1.Module.resolvedImports.TryGetValue(ic1.Id, out IImportable? elem1) && elem1 is Const rc1) {
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
               } else {
                  Debug.Assert(called is Procedure, $"CollectReachableObjects: Unknown call type {called}");
                  CollectReachableObjects(((Procedure)called).group);
               }
            }
         }
         return true;
      }
      private void CollectReachableObjects(Const constant) {
         if (constant is ImportedConst) constant = (constant.Module!.resolvedImports[constant.Id] as Const)!;
         if (Objects.Add(constant)) {
            foreach (IConstElement elem in constant.elements) {
               switch (elem) {
                  case ID id:
                     if (((Section)constant.Parent!).TryGetDeclaration(id, out CDL2Object? obj)) {
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
         foreach (IMacroElement element in macro.elements) {
            switch (element) {
               case Affix:
               case Local:
                  break;
               case ID id:
                  if (macro.Section.TryGetDeclaration(id, out CDL2Object? obj)) {
                     switch (obj) {
                        case Const c:
                           CollectReachableObjects(c);
                           break;
                        case Var v:
                           Objects.Add(v);
                           break;
                        case LIST l:
                           CollectReachableObjects(macro, l);
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
                  CollectReachableObjects(macro, l);
                  break;
            }
         }
      }
      private void CollectReachableObjects(Macro macro, LIST list) {
         if (Objects.Add(list)) {
            if (macro.Section!.TryGetDeclaration(list.lwb, out Const? lwb)) CollectReachableObjects(lwb);
            if (macro.Section.TryGetDeclaration(list.upb, out Const? upb)) CollectReachableObjects(upb);
         }
      }
   }
}
