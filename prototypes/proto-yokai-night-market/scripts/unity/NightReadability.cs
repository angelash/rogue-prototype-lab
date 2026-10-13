// Packaging-only color override. No runtime behavior or new scene components.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Newtonsoft.Json.Linq;

public static class NightReadability
{
    static JObject Spec => JObject.Parse(File.ReadAllText("../../fidelity/data/presentation.json"));
    static int Apply(GameObject[] roots)
    {
        var specs=((JObject)Spec["textColorOverrides"]).Properties().Select(p=>p.Value).ToArray();int changed=0;
        foreach(var text in roots.SelectMany(g=>g.GetComponentsInChildren<TextMesh>(true)))
        {
            var spec=specs.FirstOrDefault(s=>text.gameObject.name==(string)s["object"] && text.text==(string)s["key"]);
            if(spec==null)continue;
            var before=EditorJsonUtility.ToJson(text);var previous=text.color;
            text.color=new Color((float)spec["r"],(float)spec["g"],(float)spec["b"],(float)spec["a"]);
            var after=EditorJsonUtility.ToJson(text);var revised=text.color;
            text.color=previous;
            if(EditorJsonUtility.ToJson(text)!=before)throw new Exception("Unexpected TextMesh field mutation");
            text.color=revised;
            var a=JObject.Parse(before);var b=JObject.Parse(after);
            foreach(var item in a.DescendantsAndSelf().OfType<JObject>()){item.Remove("m_Color");item.Remove("m_Color32");}
            foreach(var item in b.DescendantsAndSelf().OfType<JObject>()){item.Remove("m_Color");item.Remove("m_Color32");}
            if(!JToken.DeepEquals(a,b))throw new Exception("Override changed a non-color TextMesh field");
            EditorUtility.SetDirty(text);changed++;
        }
        return changed;
    }
    public static void ApplyAndBuild()
    {
        NightAssetExport.ExportReadabilityFix();
        var scene=EditorSceneManager.OpenScene("Assets/Recovery/Scenes/NightMarket.unity");
        int sceneCount=Apply(scene.GetRootGameObjects());
        int expected=((JObject)Spec["textColorOverrides"]).Count;
        if(sceneCount!=expected)throw new Exception("Incomplete scene text-color overrides");
        EditorSceneManager.SaveScene(scene);
        int prefabCount=0;
        foreach(string path in Directory.GetFiles("Assets/Recovery/Prefabs","*.prefab"))
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try {int count=Apply(new[]{root});if(count>0){PrefabUtility.SaveAsPrefabAsset(root,path);prefabCount+=count;}}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        if(prefabCount!=expected)throw new Exception("Incomplete prefab text-color overrides");
        int materials=0;
        foreach(var entry in ((JObject)Spec["materialColorOverrides"]).Properties())
        {
            var value=entry.Value;var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/"+(string)value["resource"]+".asset");
            if(!mat || !mat.HasProperty("_Color"))throw new Exception("Worker label material missing");
            mat.color=new Color((float)value["r"],(float)value["g"],(float)value["b"],(float)value["a"]);EditorUtility.SetDirty(mat);materials++;
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText("../readability-report.json",new JObject{{"passed",true},{"sceneTextColors",sceneCount},{"prefabTextColors",prefabCount},{"materialColors",materials},{"otherTextMeshFieldsUnchanged",true},{"source","fidelity/data/presentation.json"}}.ToString());
        RecoveryEditor.BuildFinalCandidates();
    }
}
