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
            var before=EditorJsonUtility.ToJson(text);var previous=text.color;float previousSize=text.characterSize;
            var style=((JObject)Spec["textStyleOverrides"])?.Properties().Select(p=>p.Value).FirstOrDefault(s=>text.gameObject.name==(string)s["object"] && text.text==(string)s["key"]);
            if(style!=null)text.characterSize=(float)style["characterSize"];
            text.color=new Color((float)spec["r"],(float)spec["g"],(float)spec["b"],(float)spec["a"]);
            var after=EditorJsonUtility.ToJson(text);var revised=text.color;float revisedSize=text.characterSize;
            text.color=previous;text.characterSize=previousSize;
            if(EditorJsonUtility.ToJson(text)!=before)throw new Exception("Unexpected TextMesh field mutation");
            text.color=revised;text.characterSize=revisedSize;
            var a=JObject.Parse(before);var b=JObject.Parse(after);
            foreach(var item in a.DescendantsAndSelf().OfType<JObject>()){item.Remove("m_Color");item.Remove("m_Color32");}
            foreach(var item in b.DescendantsAndSelf().OfType<JObject>()){item.Remove("m_Color");item.Remove("m_Color32");}
            if(style!=null){foreach(var item in a.DescendantsAndSelf().OfType<JObject>())item.Remove("m_CharacterSize");foreach(var item in b.DescendantsAndSelf().OfType<JObject>())item.Remove("m_CharacterSize");}
            if(!JToken.DeepEquals(a,b)){File.WriteAllText("../readability-delta.json",new JObject{{"before",a},{"after",b}}.ToString());throw new Exception("Override changed an undeclared TextMesh field");}
            EditorUtility.SetDirty(text);changed++;
        }
        return changed;
    }
    static string HierarchyPath(Transform t) => t.parent?HierarchyPath(t.parent)+"/"+t.name:t.name;
    static int ApplyComponents(GameObject[] roots)
    {
        var specs=((JObject)Spec["componentColorOverrides"]).Properties().Select(p=>p.Value).ToArray();int count=0;
        foreach(var transform in roots.SelectMany(g=>g.GetComponentsInChildren<Transform>(true)))
        foreach(var spec in specs.Where(s=>HierarchyPath(transform)==(string)s["path"] || HierarchyPath(transform).EndsWith("/"+(string)s["path"],StringComparison.Ordinal)))
        {
            Component component=(string)spec["component"]=="SpriteRenderer"?(Component)transform.GetComponent<SpriteRenderer>():transform.GetComponent<TextOutline>();
            if(!component)throw new Exception("Missing theme component: "+spec["path"]);
            var before=JObject.Parse(EditorJsonUtility.ToJson(component));
            var serialized=new SerializedObject(component);var field=serialized.FindProperty((string)spec["field"]);
            if(field==null || field.propertyType!=SerializedPropertyType.Color)throw new Exception("Not a color field");
            Color value=new Color((float)spec["r"],(float)spec["g"],(float)spec["b"],(float)spec["a"]);
            if(Mathf.Abs(field.colorValue.a-value.a)>0.000001f)throw new Exception("Theme must preserve alpha");
            field.colorValue=value;serialized.ApplyModifiedPropertiesWithoutUndo();
            var after=JObject.Parse(EditorJsonUtility.ToJson(component));
            foreach(var item in before.DescendantsAndSelf().OfType<JObject>())item.Remove((string)spec["field"]);
            foreach(var item in after.DescendantsAndSelf().OfType<JObject>())item.Remove((string)spec["field"]);
            if(!JToken.DeepEquals(before,after)){File.WriteAllText("../style-component-delta.json",new JObject{{"spec",spec},{"before",before},{"after",after}}.ToString());throw new Exception("Undeclared component delta");}
            EditorUtility.SetDirty(component);count++;
        }
        return count;
    }
    public static void ApplyNightStyleAndBuild(){ApplyAll(true,true);}
    public static void ApplyNightColorsAndBuild(){ApplyAll(false,false);}
    public static void ApplyAndBuild()
    {
        ApplyAll(true);
    }
    public static void ApplyPromptFixAndBuild()
    {
        ApplyAll(false);
    }
    static void ApplyAll(bool frames,bool style=false)
    {
        if(style)NightAssetExport.ExportNightStyle();else if(frames)NightAssetExport.ExportReadabilityFix();else NightAssetExport.ExportPromptFix();
        var scene=EditorSceneManager.OpenScene("Assets/Recovery/Scenes/NightMarket.unity");
        int sceneCount=Apply(scene.GetRootGameObjects());
        int expected=((JObject)Spec["textColorOverrides"]).Count;
        if(sceneCount!=expected)throw new Exception("Incomplete scene text-color overrides");
        int expectedComponents=((JObject)Spec["componentColorOverrides"])?.Count??0;
        int sceneComponents=expectedComponents>0?ApplyComponents(scene.GetRootGameObjects()):0;
        if(sceneComponents!=expectedComponents)throw new Exception("Incomplete scene component colors");
        EditorSceneManager.SaveScene(scene);
        int prefabCount=0,prefabComponents=0;
        foreach(string path in Directory.GetFiles("Assets/Recovery/Prefabs","*.prefab").Select(p=>p.Replace('\\','/')))
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try {int count=Apply(new[]{root});int components=expectedComponents>0?ApplyComponents(new[]{root}):0;if(count+components>0){PrefabUtility.SaveAsPrefabAsset(root,path);prefabCount+=count;prefabComponents+=components;}}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        if(prefabCount!=expected)throw new Exception("Incomplete prefab text-color overrides: "+prefabCount+" / "+expected);
        if(prefabComponents!=expectedComponents)throw new Exception("Incomplete prefab component colors: "+prefabComponents+" / "+expectedComponents);
        int materials=0;
        foreach(var entry in ((JObject)Spec["materialColorOverrides"]).Properties())
        {
            var value=entry.Value;var mat=AssetDatabase.LoadAssetAtPath<Material>((string)value["assetPath"]??("Assets/Resources/"+(string)value["resource"]+".asset"));
            if(!mat || !mat.HasProperty("_Color"))throw new Exception("Worker label material missing");
            mat.color=new Color((float)value["r"],(float)value["g"],(float)value["b"],(float)value["a"]);EditorUtility.SetDirty(mat);materials++;
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText("../readability-report.json",new JObject{{"passed",true},{"sceneTextColors",sceneCount},{"prefabTextColors",prefabCount},{"sceneComponentColors",sceneComponents},{"prefabComponentColors",prefabComponents},{"materialColors",materials},{"textStyleOverrides",((JObject)Spec["textStyleOverrides"])?.Count??0},{"onlyDeclaredFieldsChanged",true},{"source","fidelity/data/presentation.json"}}.ToString());
        RecoveryEditor.BuildFinalCandidates();
    }
}
