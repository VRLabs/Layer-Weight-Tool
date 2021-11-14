using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.IO;
using System.Linq;
using VRC.SDK3.Avatars.Components;

namespace VRLabs.LayerWeightTool
{
    [ExecuteInEditMode]
    public class LayerWeightTool : Editor
    {
        static readonly int[] toPlayable = { 3, 4, 2, 1 }; // VRC_AnimatorLayerControl.BlendableLayer
        static readonly string path = "Assets/VRLabs/GeneratedAssets/";
        static readonly string tag = "Control ";
        public static int countFixed;
        public static int countAlreadyFixed;
        public static int countUnfixable;
        [MenuItem("VRLabs/Apply Weight Controls")]
        public static void Fix()
        {
            VRCAvatarDescriptor[] avatars = FindObjectsOfType(typeof(VRCAvatarDescriptor)) as VRCAvatarDescriptor[];
            foreach (VRCAvatarDescriptor avatar in avatars)
            {
                foreach (Animator animator in avatar.GetComponentsInChildren<Animator>(true))
                {
                    if (animator.runtimeAnimatorController == null || animator.gameObject == avatar.gameObject) { continue; }
                    AnimatorController controller = (AnimatorController)animator.runtimeAnimatorController;
                    bool hasBeenCopied = false;

                    var query = (from AnimatorControllerLayer l in controller.layers
                                    where l.name.StartsWith(tag)
                                    select l).ToArray();
                    for (int i = 0; i < query.Length; i++) //foreach (AnimatorControllerLayer l in query)
                    {
                        AnimatorControllerLayer l = query[i];
                        for (int j = 0; j < l.stateMachine.states.Length; j++)  //foreach (ChildAnimatorState state in l.stateMachine.states)
                        {
                            ChildAnimatorState state = l.stateMachine.states[j];
                            for (int k = 0; k < state.state.behaviours.Length; k++) //foreach (StateMachineBehaviour behaviour in state.state.behaviours)
                            {
                                StateMachineBehaviour behaviour = state.state.behaviours[k];
                                if (behaviour.GetType() == typeof(VRCAnimatorLayerControl))
                                {
                                    VRCAnimatorLayerControl ctrl = (VRCAnimatorLayerControl)behaviour;
                                    int layer = toPlayable[(int)ctrl.playable];
                                    if (avatar.baseAnimationLayers[layer].isDefault == true || avatar.baseAnimationLayers[layer].animatorController == null)
                                    {
                                        countUnfixable++; 
                                        continue; 
                                    }
                                    AnimatorController playable = (AnimatorController)avatar.baseAnimationLayers[layer].animatorController;
                                    int index = playable.layers.ToList().FindIndex(x => x.name.Equals(l.name.Substring(tag.Length)));
                                    if (index == -1)
                                    {
                                        countUnfixable++;
                                        continue;
                                    }
                                    if (ctrl.layer == index)
                                    {
                                        countAlreadyFixed++;
                                        continue;
                                    }
                                    if (!hasBeenCopied)
                                    {
                                        controller = MakeCopy(avatar, controller);
                                        hasBeenCopied = true;
                                    }
                                    ctrl = (VRCAnimatorLayerControl)controller.layers[i].stateMachine.states[j].state.behaviours[k];
                                    ctrl.layer = index;
                                    animator.runtimeAnimatorController = controller;
                                    countFixed++;
                                }
                            }
                        }
                    }
                }
            }
            string output = "";
            if (countFixed != 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                output += "Fixed " + countFixed + (countFixed == 1 ? " index." : " indices. ");
            }
            if (countAlreadyFixed != 0)
                output += countAlreadyFixed + (countFixed == 1 ? " index" : " indices") + " are already fixed. ";
            if (countUnfixable != 0)
                output += countUnfixable + (countUnfixable == 1 ? " index" : " indices") + " could not be fixed; the layer on the avatar was not found. ";
            if (output == "")
                output = "No valid subanimators found, nothing to fix. ";
            output += "(Searched " + avatars.Length + (avatars.Length == 1 ? " avatar)" : " avatars)");
            countFixed = 0;
            countAlreadyFixed = 0;
            countUnfixable = 0;
            Debug.Log(output);
        }
        private static AnimatorController MakeCopy(VRCAvatarDescriptor avi, AnimatorController c)
        {
            Directory.CreateDirectory(path + avi.name);
            AssetDatabase.Refresh();
            string newPath = AssetDatabase.GenerateUniqueAssetPath(path + avi.name + "/" + c.name + ".controller");
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(c), newPath);
            c = AssetDatabase.LoadAssetAtPath(newPath, typeof(AnimatorController)) as AnimatorController;
            EditorUtility.SetDirty(c);
            return c;
        }
    }
}