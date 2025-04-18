using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.Contracts;
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
      public Set<ICDL2Object> Objects {
         get {
            Debug.Assert(collecting || field.Count != 0, "Must call CollectReachebleObjects(program/module)  before accessing Objects");
            return field;
         }
         private set;
      } = [];
      public Set<ICDL2Object> AllObjects = []; // All objects in the program/module, including those not reachable from the entry point.
      // Used to track the variables that are read in the program. Write references are in <see cref="ReferencedObjects."/>.
      public Set<ICDL2Object> ReadVars { get;  private set; } = [];
      public Set<ICDL2Object> AmbigousVars { get; private set; } = [];
      
      public void CollectAllObjects(Program prog) {
         AllObjects = [];
         foreach (Module? mod in prog.Parts.Select(id => Database.Instance.Modules.TryGetValue(id,out Module? mod)?mod:null)) {
            if (mod is not null) {
               foreach (Layer lay in mod.Children.Cast<Layer>()) {
                  foreach (Section sec in lay.Children.Cast<Section>()) {
                     foreach (ICDL2Object obj in sec.declarations.Values) {
                        AllObjects.Add(obj);
                     }
                  }
               }
            }
         }
         Logger.Log(0, $"Collected {AllObjects.Count} objects from {prog}.");
      }

      public void CollectReachableObjects(Program prog) {
         Objects = [];
         ReadVars = [];
         AmbigousVars = [];
         collecting = true;
         Logger.Log(0, $"Collecting objects reachable from {prog} ...");
         foreach (RW ludeType in Container.LudeTypes) {
            foreach (ID id in prog.Ludes[ludeType]) {
               if (Database.Instance.Modules.TryGetValue(id, out Module? module)) {
                  CollectReachableObjects(ludeType, module);
               }
            }
         }
         collecting = false;
         string CountObjects(Type type) => Objects.Where(obj => obj.GetType() == type).Count().Plural(type.Name);
         Logger.Log(0, $"{CountObjects(typeof(Const))}, {CountObjects(typeof(Var))}, {CountObjects(typeof(LIST))}, {CountObjects(typeof(Macro))}, {CountObjects(typeof(Procedure))} collected.");
      }
      public void CollectReachableObjects(Module module) => throw new NotImplementedException($"CollectReachableObjects: Not yet implemented for modules.");
      private void CollectReachableObjects(RW Ludetype, Module module) {
         foreach (Section? section in module.Ludes[Ludetype].Select(id => module.Section(id))) {
            if (section is not null) CollectReachableObjects(Ludetype, section);
         }
      }
      private void CollectReachableObjects(RW ludetype, Section section) {
         // Section ludes contain teh single entry of argAffix synthetic procedure that is the lude
         // So we need to collect all the objects in the section that are reachable from this lude.
         Debug.Assert(section.Ludes[ludetype].Count == 1, $"CollectReachableObjects: Expected single lude in {section}");
         if (section.TryGetDeclaration(section.Ludes[ludetype][0], out Procedure? proc)) {
            if (Objects.Add(proc!)) CollectReachableObjects(proc!.group);
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
      /// Return false if the rest of the alternative contining the call is to be ignored.
      /// </summary>
      /// <param name="call"></param>
      /// <returns></returns>
      private bool CollectReachableObjects(Call call) {
         if (call.Called is not null) {
            Algorithm called = call.Called;
            if (called is ImportedAlgorithm importedAlg) {
               // TODO: Find imported algorithm and assign it to called.
            }
            if (called.IsConditionalCompilationOn) return true;   // Ignore
            if (called.IsConditionalCompilationOff) return false; // Skip the rest of the alternative.

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
                     if (call.Called.Section.TryGetDeclaration(id, out ICDL2DataObject? obj)) {
                        if (obj is Const c) {
                           CollectReachableObjects(c);
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
         if (Objects.Add(constant)) {
            foreach (IConstElement elem in constant.elements) {
               switch (elem) {
                  case ID id:
                     if (((Section)constant.Parent!).TryGetDeclaration(id, out ICDL2DataObject? obj)) {
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
                  if (macro.Section.TryGetDeclaration(id, out ICDL2DataObject? obj)) {
                     switch (obj) {
                        case ImportedConst ic:
                           // TODO: Find imported constant.
                           break;
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
               case ImportedConst ic:
                  // TODO: Find imported constant.
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
            if (macro.Section.TryGetDeclaration(list.lwb, out ICDL2DataObject? lwbObj) && lwbObj is Const lwb) CollectReachableObjects(lwb);
            if (macro.Section.TryGetDeclaration(list.upb, out ICDL2DataObject? upbObj) && lwbObj is Const upb) CollectReachableObjects(upb);
         }
      }
   }
}
