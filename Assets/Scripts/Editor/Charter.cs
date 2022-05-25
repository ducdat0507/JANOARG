using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.Globalization;

public class Charter : EditorWindow
{
    [MenuItem("J.A.N.O.A.R.G./Charter")]
    public static void Open()
    {
        Charter wnd = GetWindow<Charter>();
        wnd.titleContent = new GUIContent("Charter");
        wnd.minSize = new Vector2(960, 600);
    }

    public static void Open(PlayableSong target)
    {
        Charter wnd = GetWindow<Charter>();
        wnd.titleContent = new GUIContent("Charter");
        wnd.minSize = new Vector2(960, 600);
        wnd.TargetSong = target;
    }

    public PlayableSong TargetSong;
    public Chart TargetChart;
    public Lane TargetLane;
    public object TargetThing;
    public object DeletingThing;

    public AudioSource CurrentAudioSource;
    public Camera CurrentCamera;
    public AudioClip MetronomeSound;

    public bool PlayMetronome;
    public bool SeparateUnits;

    CultureInfo invariant = CultureInfo.InvariantCulture;

    float width, height, pos, dec, beat, currentBeat, bar, min, sec, ms;

    public void OnDestroy() 
    {
        Destroy(CurrentAudioSource);
        Destroy(CurrentCamera);
    }

    // Literally a miracle
    public Mesh MakeLaneMesh(Lane lane) 
    {
        Mesh mesh = new Mesh();

        float pos = 0;
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        void AddStep(Vector3 start, Vector3 end) {

            vertices.Add(start);
            vertices.Add(end);
            vertices.Add(start);
            vertices.Add(end);

            uvs.Add(Vector2.zero);
            uvs.Add(Vector2.zero);
            uvs.Add(Vector2.zero);
            uvs.Add(Vector2.zero);
            
            if (vertices.Count >= 8) 
            {
                tris.Add(vertices.Count - 1);
                tris.Add(vertices.Count - 5);
                tris.Add(vertices.Count - 6);
                
                tris.Add(vertices.Count - 6);
                tris.Add(vertices.Count - 2);
                tris.Add(vertices.Count - 1);

                tris.Add(vertices.Count - 8);
                tris.Add(vertices.Count - 7);
                tris.Add(vertices.Count - 3);
                
                tris.Add(vertices.Count - 3);
                tris.Add(vertices.Count - 4);
                tris.Add(vertices.Count - 8);
            }
        }

        float curtime = CurrentAudioSource.time;

        float lastP = 0;

        List<LaneStep> steps = new List<LaneStep>();

        for (int a = 0; a < lane.LaneSteps.Count; a++)
            steps.Add((LaneStep)lane.LaneSteps[a].Get(this.pos));

        for (int a = 0; a < steps.Count; a++)
        {
            LaneStep step = steps[a];

            float time = TargetSong.Timing.ToSeconds(step.Offset);
            Vector3 start = step.StartPos;
            Vector3 end = step.EndPos;
            float p = 0;
            if (CurrentAudioSource.time > time) 
            {
                if (a >= steps.Count - 1) {
                    lastP = 1;
                    continue;
                }
                LaneStep next = steps[a + 1];
                float nexttime = TargetSong.Timing.ToSeconds(next.Offset);
                if (CurrentAudioSource.time > nexttime) 
                {
                    lastP = 1;
                    continue;
                }
                p = (curtime - time) / (nexttime - time);
                // Debug.Log("P " + a + " " + p);
                start = new Vector2(Mathf.Lerp(step.StartPos.x, next.StartPos.x, Ease.Get(p, next.StartEaseX, next.StartEaseXMode)),
                    Mathf.Lerp(step.StartPos.y, next.StartPos.y, Ease.Get(p, next.StartEaseY, next.StartEaseYMode)));
                end = new Vector2(Mathf.Lerp(step.EndPos.x, next.EndPos.x, Ease.Get(p, next.EndEaseX, next.EndEaseXMode)),
                    Mathf.Lerp(step.EndPos.y, next.EndPos.y, Ease.Get(p, next.EndEaseY, next.EndEaseYMode)));
            }

            float lPos = pos;
            pos += step.Speed * 120 * (Mathf.Max(time, CurrentAudioSource.time) - curtime);
            curtime = Mathf.Max(time, CurrentAudioSource.time);
            if (a == 0) 
            {
                AddStep(new Vector3(start.x, start.y, pos), new Vector3(end.x, end.y, pos));
            }
            else 
            {
                LaneStep prev = steps[a - 1];
                if (lastP >= 1 || (step.StartEaseX == "Linear" && step.StartEaseY == "Linear" &&
                    step.EndEaseX == "Linear" && step.EndEaseY == "Linear"))
                {
                    AddStep(new Vector3(start.x, start.y, pos), new Vector3(end.x, end.y, pos));
                }
                else
                {
                    // Debug.Log("T " + step.StartEaseX + " " + p);
                    for (float x = lastP; x <= 1; x = Mathf.Floor(x * 16 + 1.01f) / 16)
                    {
                        float cPos = Mathf.Lerp(lPos, pos, (x - lastP) / (1 - lastP));
                        start = new Vector3(Mathf.Lerp(prev.StartPos.x, step.StartPos.x, Ease.Get(x, step.StartEaseX, step.StartEaseXMode)),
                            Mathf.Lerp(prev.StartPos.y, step.StartPos.y, Ease.Get(x, step.StartEaseY, step.StartEaseYMode)), cPos);
                        end = new Vector3(Mathf.Lerp(prev.EndPos.x, step.EndPos.x, Ease.Get(x, step.EndEaseX, step.EndEaseXMode)),
                            Mathf.Lerp(prev.EndPos.y, step.EndPos.y, Ease.Get(x, step.EndEaseY, step.EndEaseYMode)), cPos);
                        AddStep(start, end);
                    }
                }
            }

            lastP = p;
        }

        mesh.Clear();
        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();

        return mesh;
    }

    public Mesh MakeHitMesh(HitObject hit, Lane lane, out Vector2 startPos, out Vector2 endPos) 
    {
        LaneStep step = lane.GetLaneStep(hit.Offset, pos, TargetSong.Timing);
        float len = Mathf.Max(hit.Length, .2f / Vector3.Distance(step.StartPos, step.EndPos));
        startPos = Vector2.LerpUnclamped(step.StartPos, step.EndPos, hit.Position);
        endPos = Vector2.LerpUnclamped(step.StartPos, step.EndPos, hit.Position + len);

        Mesh mesh = new Mesh();
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();

        if (Mathf.Abs(step.Offset) > 5) return mesh;

        void AddStep(Vector3 start, Vector3 end) {

            vertices.Add(start);
            vertices.Add(end);
            vertices.Add(start);
            vertices.Add(end);

            uvs.Add(Vector2.zero);
            uvs.Add(Vector2.zero);
            uvs.Add(Vector2.zero);
            uvs.Add(Vector2.zero);
            
            if (vertices.Count >= 8) 
            {
                tris.Add(vertices.Count - 1);
                tris.Add(vertices.Count - 5);
                tris.Add(vertices.Count - 6);
                
                tris.Add(vertices.Count - 6);
                tris.Add(vertices.Count - 2);
                tris.Add(vertices.Count - 1);

                tris.Add(vertices.Count - 8);
                tris.Add(vertices.Count - 7);
                tris.Add(vertices.Count - 3);
                
                tris.Add(vertices.Count - 3);
                tris.Add(vertices.Count - 4);
                tris.Add(vertices.Count - 8);
            }
        }
        float angle = Vector2.SignedAngle(step.EndPos - step.StartPos, Vector2.left);
        Vector3 fwd = Vector3.forward * step.Offset * 120;
        for (float ang = 45; ang <= 405; ang += 90) 
        {
            Vector3 ofs = Quaternion.Euler(0, 0, -angle) 
                * new Vector3(0, Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad)) 
                * .2f;
            AddStep((Vector3)startPos + ofs + fwd, (Vector3)endPos + ofs + fwd);
        }

        mesh.Clear();
        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();

        return mesh;
    }

    public void OnGUI()
    {
        if (!CurrentCamera) {
            CurrentCamera = new GameObject("Charter Camera").AddComponent<Camera>();
            CurrentCamera.clearFlags = CameraClearFlags.SolidColor;
            CurrentCamera.targetDisplay = 8;
            CurrentCamera.gameObject.hideFlags = HideFlags.HideAndDontSave;
        }
        if (!CurrentAudioSource) {
            CurrentAudioSource = new GameObject("Charter Audio").AddComponent<AudioSource>();
            CurrentAudioSource.gameObject.hideFlags = HideFlags.HideAndDontSave;
        }
        if (!MetronomeSound) {
            MetronomeSound = Resources.Load<AudioClip>("Sounds/Metronome");
        }


        width = position.width;
        height = position.height;

        if (TargetSong) {
            pos = TargetSong.Timing.ToBeat(CurrentAudioSource.time);
            dec = Mathf.Floor((pos % 1) * 1000);
            beat = Mathf.Floor(TargetSong.Timing.ToDividedBeat(CurrentAudioSource.time));
            bar = Mathf.Floor(TargetSong.Timing.ToBar(CurrentAudioSource.time));
            
            min = Mathf.Floor(CurrentAudioSource.time / 60);
            sec = Mathf.Floor(CurrentAudioSource.time % 60);
            ms = Mathf.Floor((CurrentAudioSource.time % 1) * 1000);
            
            if ((TargetThing is PlayableSong && TargetThing != (object)TargetSong) || 
                (TargetThing is Chart && TargetThing != (object)TargetChart)) 
                TargetThing = null;
            if (TargetSong.Charts.IndexOf(TargetChart) < 0) TargetChart = null;
            if (TargetChart == null || TargetChart.Lanes.IndexOf(TargetLane) < 0) TargetLane = null;

            Rect bound = new Rect(45, 35, width - 320, height - 202);
            if (bound.width / bound.height > 3 / 2f) 
            {
                float width = (bound.height * 3 / 2);
                bound.x = bound.x + (bound.width - width) / 2;
                bound.width = width;
            } 
            else {
                float height = (bound.width / 3 * 2);
                bound.y = bound.y + (bound.height - height) / 2;
                bound.height = height;
            }

            float camLeft = (bound.center.x - (width - bound.center.x));
            float camRatio = (bound.height / (height - 184));


            if (TargetChart != null) 
            {
                Chart chart = (Chart)TargetChart.Get(pos);
                CurrentCamera.transform.position = chart.CameraPivot;
                CurrentCamera.transform.eulerAngles = chart.CameraRotation;
                CurrentCamera.transform.Translate(Vector3.back * 10);
                CurrentCamera.fieldOfView = Mathf.Atan2(Mathf.Tan(30 * Mathf.Deg2Rad), camRatio) * 2 * Mathf.Rad2Deg;
                CurrentCamera.backgroundColor = chart.BackgroundColor;
                CurrentCamera.Render();
                foreach (Lane lane in chart.Lanes)
                {
                    if (chart.LaneMaterial) {
                        Graphics.DrawMesh(MakeLaneMesh(lane), Vector3.zero, Quaternion.identity, chart.LaneMaterial, 0, CurrentCamera);
                    }
                    if (chart.HitMaterial) {
                        foreach (HitObject hit in lane.Objects)
                        {
                            if (hit.Offset > pos)
                            {
                                Mesh mesh = MakeHitMesh(hit, lane, out Vector2 startPos, out Vector2 endPos);
                                Graphics.DrawMesh(mesh, Vector3.zero, Quaternion.identity, chart.HitMaterial, 0, CurrentCamera);
                            }
                        } 
                    }
                }
                EditorGUI.DrawRect(new Rect(0, 0, width, height), CurrentCamera.backgroundColor);
                Handles.DrawCamera(new Rect(0, 26, width + camLeft, height - 184), CurrentCamera);
                Handles.color = chart.InterfaceColor;
                Handles.DrawPolyLine(new Vector2(bound.x, bound.y), new Vector2(bound.x + bound.width, bound.y), 
                    new Vector2(bound.x + bound.width, bound.y + bound.height), new Vector2(bound.x, bound.y + bound.height),
                    new Vector2(bound.x, bound.y));
            }


        }
        else 
        {
            EditorGUI.DrawRect(new Rect(0, 0, width, height), Color.black);
        }

        BeginWindows();
        if (TargetSong)
        {
            GUI.Button(new Rect(0, 0, width, 30), "", "toolbar");
            GUI.Button(new Rect(0, 6, width, 30), "", "toolbar");
            GUI.Window(1, new Rect(-2, -2, width + 4, 30), Toolbar, "", "toolbar");

            GUI.Window(2, new Rect(-2, height - 158, width + 4, 160), Timeline, "");

            GUI.Window(3, new Rect(width - 270, 36, height - 204, height - 204), InspectMode, "", new GUIStyle("button") { clipping = TextClipping.Overflow });
            GUI.BringWindowToBack(3);

            GUI.Window(4, new Rect(width - 245, 32, 240, height - 196), Inspector, "");

            GUI.Window(5, new Rect(5, 32, 32, height - 196), Picker, "");
        }
        else 
        {
            GUI.Window(1, new Rect(width / 2 - 250, height / 2 - 110, 500, 220), CharterInit, "");
        }
        EndWindows();

        if (CurrentAudioSource.isPlaying) {
            if (currentBeat != Mathf.Floor(pos)) {
                currentBeat = Mathf.Floor(pos);
                if (PlayMetronome) CurrentAudioSource.PlayOneShot(MetronomeSound);
            }
            Repaint();
        }
    }



    ///////////////////
    #region Init Window
    ///////////////////

    string initName, initArtist;
    AudioClip initClip;

    public void CharterInit(int id) {
        
        GUIStyle title = new GUIStyle(EditorStyles.largeLabel);
        title.fontSize = 20;
        title.alignment = TextAnchor.MiddleCenter;
        title.fontStyle = FontStyle.Bold;
        GUI.Label(new Rect(0, 5, 500, 40), "Welcome to J.A.N.O.A.R.G. Charter Engine", title);

        EditorGUIUtility.labelWidth = 50;
        
        title = new GUIStyle("boldLabel");
        title.alignment = TextAnchor.MiddleCenter;
        
        GUI.Label(new Rect(20, 60, 210, 40), "Edit an existing playable song:", title);
        TargetSong = (PlayableSong)EditorGUI.ObjectField(new Rect(20, 100, 210, 20), TargetSong, typeof(PlayableSong), false);
        GUIStyle label = new GUIStyle("miniLabel");
        label.alignment = TextAnchor.MiddleCenter;
        label.wordWrap = true;
        GUI.Label(new Rect(20, 122, 210, 20), "(select a playable song to continue)", label);

        GUI.Label(new Rect(270, 40, 210, 40), "or create a new one:", title);
        initName = EditorGUI.TextField(new Rect(270, 80, 210, 20), "Name", initName);
        initArtist = EditorGUI.TextField(new Rect(270, 102, 210, 20), "Artist", initArtist);
        initClip = (AudioClip)EditorGUI.ObjectField(new Rect(270, 124, 210, 20), "Clip", initClip, typeof(AudioClip), false);
    
        if (GUI.Button(new Rect(270, 146, 210, 20), "Create Playable Song"))
        {
            PlayableSong song = ScriptableObject.CreateInstance<PlayableSong>();
            song.SongName = initName;
            song.SongArtist = initArtist;
            song.Clip = initClip;

            string path = AssetDatabase.GetAssetPath(Selection.activeInstanceID);
            if (!System.IO.Directory.Exists(path)) path = System.IO.Path.GetDirectoryName(path);

            AssetDatabase.CreateAsset(song, AssetDatabase.GenerateUniqueAssetPath(path + "/" + initName + " - " + initArtist + ".asset"));
            AssetDatabase.SaveAssets();

            TargetSong = song;
        }

        label.fontStyle = FontStyle.Italic;
        GUI.Label(new Rect(0, 190, 500, 20), "J.A.N.O.A.R.G.    © 2022-2022    by FFF40 Studios", label);
    }

    #endregion

    //////////////////////
    #region Toolbar Window
    //////////////////////

    public void Toolbar(int id) {
        TargetSong = (PlayableSong)EditorGUI.ObjectField(new Rect(133, 5, 21, 20), TargetSong, typeof(PlayableSong), false);

        if (GUI.Toggle(new Rect(5, 5, 130, 20), TargetThing == (object)TargetSong, TargetSong.SongName, "buttonLeft") && TargetThing != (object)TargetSong) {
            TargetThing = TargetSong;
        }
                
        List<string> sels = new List<string>();
        foreach (Chart chart in TargetSong.Charts) sels.Add(chart.DifficultyName + " " + chart.DifficultyLevel);
        int sel = TargetChart != null ? EditorGUI.Popup(new Rect(287, 5, 18, 20), -1, sels.ToArray(), "buttonRight") :
            EditorGUI.Popup(new Rect(157, 5, 148, 20), -1, sels.ToArray(), "button");
        if (TargetChart == null) 
        {
            GUIStyle style = new GUIStyle("label");
            style.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(157, 5, 148, 20), "Select Chart...", style);
        }
        if (sel >= 0) TargetChart = TargetSong.Charts[sel];
        
        if (TargetChart != null && GUI.Toggle(new Rect(157, 5, 130, 20), TargetThing == (object)TargetChart, TargetChart.DifficultyName + " " + TargetChart.DifficultyLevel, "buttonLeft") && TargetThing != (object)TargetChart) {
            TargetThing = TargetChart;
        }

        if (GUI.Button(new Rect(position.width / 2 - 20, 1, 40, 28), EditorGUIUtility.IconContent(CurrentAudioSource.isPlaying ? "PauseButton" : "PlayButton"), "buttonMid")) 
        {
            if (CurrentAudioSource.isPlaying) 
            {
                CurrentAudioSource.Pause();
            }
            else 
            {
                CurrentAudioSource.clip = TargetSong.Clip;
                CurrentAudioSource.Play();
            }
        }

        if (GUI.Button(new Rect(position.width / 2 - 66, 5, 40, 20), new GUIContent("Save", "Save Chart")))
        {
            EditorUtility.SetDirty(TargetSong);
        }

        PlayMetronome = GUI.Toggle(new Rect(position.width / 2 + 24, 5, 40, 20), PlayMetronome, new GUIContent("Met", "Metronome"), "buttonLeft");
        SeparateUnits = GUI.Toggle(new Rect(position.width / 2 + 65, 5, 40, 20), SeparateUnits, new GUIContent("Sep", "Separate Units"), "buttonRight");



        GUIStyle counter = new GUIStyle("label");
        counter.alignment = TextAnchor.MiddleCenter;
        counter.fontStyle = FontStyle.Italic;
        counter.fontSize = 14;
        
        string ctText = SeparateUnits ? min.ToString("00", invariant) + ":" + sec.ToString("00", invariant) + "s" + ms.ToString("000", invariant) : CurrentAudioSource.time.ToString("0.000", invariant).Replace(".", "s");
        float counterX = width - 84;
        for (int a = ctText.Length - 1; a >= 0; a--)
        {
            GUI.Label(new Rect(counterX, 6, 15, 20), ctText[a].ToString(), counter);
            counterX -= 8;
        }

        counterX -= 10;

        ctText = SeparateUnits ? bar.ToString("0", invariant) + ":" + beat.ToString("00", invariant) + "b" + dec.ToString("000", invariant) : pos.ToString("0.000", invariant).Replace(".", "b");
        counter.fontSize = 18;
        for (int a = ctText.Length - 1; a >= 0; a--)
        {
            GUI.Label(new Rect(counterX, 5, 15, 20), ctText[a].ToString(), counter);
            counterX -= 10;
        }

        EditorGUI.DrawRect(new Rect(width - 64, 6, 62, 18), Color.black);
        BPMStop bstop = TargetSong.Timing.GetStop(CurrentAudioSource.time);
        EditorGUI.DrawRect(new Rect(width - 63 + beat * 60 / bstop.Signature, 7, 60 / bstop.Signature, 16), Color.white * (1 - dec / 1000));

    }

    #endregion

    ///////////////////////
    #region Timeline Window
    ///////////////////////

    public float seekStart, seekEnd;

    public string dragMode = "";
    public bool dragged = false;
    public string timelineMode = "lane";

    public void Timeline(int id) {
        float seekLimit = TargetSong.Timing.ToBeat(TargetSong.Clip.length) + 4;
        float seekTime = TargetSong.Timing.ToBeat(CurrentAudioSource.time);
        if (seekEnd == seekStart && seekStart == 0) 
        {
            seekEnd = Mathf.Min(width / 100, seekLimit);
        }

        // Category

        if (GUI.Toggle(timelineMode == "lane" ? new Rect(5, 132, 80, 24) : new Rect(5, 136, 80, 20), timelineMode == "lane", "Lanes", "button")) timelineMode = "lane";

        if (TargetLane != null && GUI.Toggle(timelineMode == "hit" ? new Rect(87, 132, 80, 24) : new Rect(87, 136, 80, 20), timelineMode == "hit", "Hits", "buttonLeft")) timelineMode = "hit";
        

        CurrentAudioSource.pitch = EditorGUI.Slider(new Rect(width - 202, 136, 200, 20), CurrentAudioSource.pitch, 0, 4);
        if (CurrentAudioSource.pitch > .95f && CurrentAudioSource.pitch < 1.05f) CurrentAudioSource.pitch = 1;


        GUIStyle label = new GUIStyle("miniLabel");
        label.alignment = TextAnchor.MiddleCenter;

        float zoom = width / (seekEnd - seekStart);
        float sep = Mathf.Log(zoom / 20, 2);
        float opa = ((sep % 1) + 1) % 1;
        sep = Mathf.Pow(2, Mathf.Floor(-sep));

        EditorGUI.DrawRect(new Rect(0, 100, width + 4, 1), EditorGUIUtility.isProSkin ? new Color(0, 0, 0, .5f) : new Color(1, 1, 1, .5f));
        for (float a = Mathf.Ceil(seekStart / sep) * sep; a < seekEnd; a += sep)
        {
            float pos = (a - seekStart) / (seekEnd - seekStart) * width;

            float op = .5f;
            if (a % (sep * 2) != 0) op *= opa;

            float op2 = 1;
            if (a % (sep * 8) != 0) op2 = 0;
            else if (a % (sep * 16) != 0) op2 *= opa;

            if (TargetSong.Timing.ToBar(0, a) % 1 == 0) 
            {
                EditorGUI.DrawRect(new Rect(pos + 1, 0, 1, 115), new Color(.6f, .6f, .4f, .5f * op));
            }
            else 
            {
                EditorGUI.DrawRect(new Rect(pos + 1.5f, 0, 1, 100), new Color(.5f, .5f, .5f, .5f * op));
            }
            
            label.normal.textColor = new Color(label.normal.textColor.r, label.normal.textColor.g, label.normal.textColor.b, op2);
            if (op2 > 0) GUI.Label(new Rect(pos - 48, 100, 100, 15), 
                SeparateUnits ? Mathf.Floor(TargetSong.Timing.ToBar(0, a)).ToString("0", invariant) + ":" + 
                Mathf.Abs(TargetSong.Timing.ToDividedBeat(0, a)).ToString("00.###", invariant) : a.ToString("0.###", invariant), label);
            //GUI.Label(new Rect(pos - 48, 100, 100, 15), , label);
        }

        EditorGUI.DrawRect(new Rect(0, 115, width + 4, 18), EditorGUIUtility.isProSkin ? new Color(0, 0, 0, .5f) : new Color(1, 1, 1, .5f));
        EditorGUI.MinMaxSlider(new Rect(2, 118, width, 15), ref seekStart, ref seekEnd, -4, seekLimit);

        EditorGUI.DrawRect(new Rect((seekTime + 4) / (seekLimit + 4) * (width - 12) + 7, 116, 1, 14), EditorGUIUtility.isProSkin ? Color.white : Color.black);
        EditorGUI.DrawRect(new Rect((seekTime + 4) / (seekLimit + 4) * (width - 12) + 7, 122, 1, 10), 
            (EditorGUIUtility.isProSkin ^ (seekTime >= seekStart && seekTime < seekEnd)) ? new Color(.9f, .9f, .9f, .75f) : new Color(.2f, .2f, .2f, .75f));

        if (TargetChart != null) {

            List<float> Times = new List<float>();
            int AddTime(float pos, float size) {
                for (int a = 0; a < Times.Count; a++)
                {
                    if (pos - Times[a] > 0) 
                    {
                        Times[a] = pos + size;
                        return a;
                    }
                }
                Times.Add(pos + size);
                return Times.Count - 1;
            }

            if (timelineMode == "lane") 
            {
                foreach (Lane lane in TargetChart.Lanes) 
                {
                    if (lane.LaneSteps.Count > 0) 
                    {
                        float a = lane.LaneSteps[0].Offset;
                        float b = lane.LaneSteps[lane.LaneSteps.Count - 1].Offset;
                        float pos2 = (b - seekStart) / (seekEnd - seekStart) * width;
                        int time = AddTime(pos2, 21);
                        if (b > seekStart && a < seekEnd) 
                        {
                            float pos = (a - seekStart) / (seekEnd - seekStart) * width;
                            EditorGUI.DrawRect(new Rect(pos, 5 + time * 25, pos2 - pos, 20), new Color(0, 1, 0, .1f));
                        }
                        for (int x = 1; x < lane.LaneSteps.Count; x++) {
                            float c = lane.LaneSteps[x].Offset;
                            if (c > seekStart && c < seekEnd) 
                            {
                                float pos3 = (c - seekStart) / (seekEnd - seekStart) * width;
                                if (GUI.Button(new Rect(pos3 - 2, 5 + time * 25, 6, 20), DeletingThing == lane.LaneSteps[x] ? "?" : ""))
                                {
                                    if (pickermode == "delete")
                                    {
                                        if (DeletingThing == lane.LaneSteps[x])
                                        {
                                            lane.LaneSteps.Remove(lane.LaneSteps[x]);
                                            break;
                                        }
                                        else
                                        {
                                            DeletingThing = lane.LaneSteps[x];
                                        }
                                    }
                                    else 
                                    {
                                        TargetThing = lane.LaneSteps[x];
                                        DeletingThing = null;
                                    }
                                }
                            }
                        }
                        if (a > seekStart && a < seekEnd) 
                        {
                            float pos = (a - seekStart) / (seekEnd - seekStart) * width;
                            if (GUI.Button(new Rect(pos - 9, 5 + time * 25, 20, 20), DeletingThing == lane ? "?" : ""))
                            {
                                if (pickermode == "delete")
                                {
                                    if (DeletingThing == lane)
                                    {
                                        TargetChart.Lanes.Remove(lane);
                                        TargetThing = TargetLane = null;
                                        break;
                                    }
                                    else
                                    {
                                        DeletingThing = lane;
                                    }
                                }
                                else 
                                {
                                    TargetThing = TargetLane = lane;
                                    DeletingThing = null;
                                }
                            }
                        }
                    }
                    else 
                    {
                        TargetChart.Lanes.Remove(lane);
                    }
                }
            }
            else if (timelineMode == "hit" && TargetLane != null)
            {
                float a = TargetLane.LaneSteps[0].Offset;
                float b = TargetLane.LaneSteps[TargetLane.LaneSteps.Count - 1].Offset;
                if (a > seekStart) 
                {
                    float pos = (a - seekStart) / (seekEnd - seekStart) * width;
                    EditorGUI.DrawRect(new Rect(0, 0, pos + 2, 115), new Color(0, 0, 0, .25f));
                }
                if (b < seekEnd) 
                {
                    float pos = (b - seekStart) / (seekEnd - seekStart) * width;
                    EditorGUI.DrawRect(new Rect(pos + 2, 0, width - pos + 2, 115), new Color(0, 0, 0, .25f));
                }
                foreach (HitObject hit in TargetLane.Objects) 
                {
                    float x = hit.Offset;
                    float pos = (x - seekStart) / (seekEnd - seekStart) * width;
                    int time = AddTime(pos, 21);
                    if (hit.Offset > seekStart && hit.Offset < seekEnd) 
                    {
                        if (GUI.Button(new Rect(pos - 9, 5 + time * 25, 20, 20), DeletingThing == hit ? "?" : ""))
                        {
                            if (pickermode == "delete")
                            {
                                if (DeletingThing == hit)
                                {
                                    TargetLane.Objects.Remove(hit);
                                    TargetThing = null;
                                    break;
                                }
                                else
                                {
                                    DeletingThing = hit;
                                }
                            }
                            else 
                            {
                                TargetThing = hit;
                                DeletingThing = null;
                            }
                        }
                    }
                }
            }
        }

        
        // Click events

        if (Event.current.type == EventType.MouseDown && Event.current.button == 0) 
        {
            Vector2 mPos = Event.current.mousePosition;
            float sPos = mPos.x * (seekEnd - seekStart) / width + seekStart;
            if (mPos.y > 100 && mPos.y < 115) 
            {
                CurrentAudioSource.time = Mathf.Clamp(TargetSong.Timing.ToSeconds(sPos), 0, TargetSong.Clip.length - .0001f);
                dragMode = "seek";
                Repaint();
            }
            else if (mPos.y > 0 && mPos.y < 100) 
            {
                CurrentAudioSource.time = Mathf.Clamp(TargetSong.Timing.ToSeconds(Mathf.Round(sPos / sep) * sep), 0, TargetSong.Clip.length - .0001f);
                dragMode = "seeksnap";
                Repaint();
            }
            dragged = false;
        }
        else if (Event.current.type == EventType.MouseDrag && Event.current.button == 0) 
        {
            Vector2 mPos = Event.current.mousePosition;
            float sPos = mPos.x * (seekEnd - seekStart) / width + seekStart;
            if (dragMode == "seek") 
            {
                CurrentAudioSource.time = Mathf.Clamp(TargetSong.Timing.ToSeconds(sPos), 0, TargetSong.Clip.length - .0001f);
                Repaint();
            }
            if (dragMode == "seeksnap") 
            {
                CurrentAudioSource.time = Mathf.Clamp(TargetSong.Timing.ToSeconds(Mathf.Round(sPos / sep) * sep), 0, TargetSong.Clip.length - .0001f);
                Repaint();
            }
            dragged = true;
        }
        else if (Event.current.type == EventType.MouseUp && Event.current.button == 0) 
        {
            if (!dragged && !CurrentAudioSource.isPlaying) 
            {
                if (dragMode == "seeksnap" && pickermode == "lane") 
                {
                    Lane lane = new Lane();
                    LaneStep step = new LaneStep();
                    step.Offset = Mathf.Round(pos * 1000) / 1000;
                    step.StartPos = new Vector2(-6, -3);
                    step.EndPos = new Vector2(6, -3);
                    lane.LaneSteps.Add(step);
                    TargetChart.Lanes.Add(lane);
                    TargetChart.Lanes.Sort((x, y) => x.LaneSteps[0].Offset.CompareTo(y.LaneSteps[0].Offset));
                    Repaint();
                }
                else if (dragMode == "seeksnap" && pickermode == "hit_normal" && TargetLane != null) 
                {
                    HitObject hit = new HitObject();
                    TargetLane.Objects.Add(hit);
                    hit.Offset = Mathf.Round(pos * 1000) / 1000;
                    TargetLane.Objects.Sort((x, y) => x.Offset.CompareTo(y.Offset));
                    Debug.Log("Made hit");
                    Repaint();
                }
            }
            dragMode = "";
        }

        if (seekTime >= seekStart && seekTime < seekEnd) {
            float pos = (seekTime - seekStart) / (seekEnd - seekStart) * width;
            EditorGUI.DrawRect(new Rect(pos + 1, 0, 2, 115), EditorGUIUtility.isProSkin ? Color.white : Color.black);
        }
    }

    #endregion

    ////////////////////////
    #region Inspect Mode Window
    ////////////////////////

    string inspectMode = "properties";

    public void InspectMode(int id) {
        GUIUtility.RotateAroundPivot(-90, Vector2.one * (height / 2 - 102));
        if (GUI.Toggle(new Rect(27, 0, 80, 28), inspectMode == "properties", "Properties", "button")) inspectMode = "properties";
        if (GUI.Toggle(new Rect(109, 0, 80, 28), inspectMode == "storyboard", "Storyboard", "button")) inspectMode = "storyboard";
    }

    #endregion

    ////////////////////////
    #region Inspector Window
    ////////////////////////

    Vector2 scrollPos = Vector2.zero;

    public void Inspector(int id) {
        GUI.Label(new Rect(0, 0, 240, 24), "", "button");
        EditorGUIUtility.labelWidth = 80;
        if (TargetThing == null) 
        {
            GUI.Label(new Rect(7, 2, 226, 20), "No object selected", "boldLabel");
            GUILayout.Space(8);
            GUILayout.Label("Please select an object to start editing.");
        }
        else if (inspectMode == "properties")
        {
            if (TargetThing is PlayableSong)
            {
                PlayableSong thing = (PlayableSong)TargetThing;

                GUIStyle bStyle = new GUIStyle("textField");
                bStyle.fontStyle = FontStyle.Bold;

                GUI.Label(new Rect(7, 2, 226, 20), "Song Details", "boldLabel");
                GUILayout.Space(8);
                scrollPos = GUILayout.BeginScrollView(scrollPos);
                GUILayout.Label("Metadata", "boldLabel");
                thing.SongName = EditorGUILayout.TextField("Song Name", thing.SongName, bStyle);
                thing.SongArtist = EditorGUILayout.TextField("Song Artist", thing.SongArtist);
                GUILayout.Space(8);
                GUILayout.Label("Charts", "boldLabel");
                foreach (Chart chart in TargetSong.Charts)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Toggle(TargetChart == chart, chart.DifficultyName + " " + chart.DifficultyLevel, "ButtonLeft"))
                    {
                        TargetChart = chart;
                    }
                    if (GUILayout.Button("x", "ButtonRight", GUILayout.MaxWidth(18)) && TargetChart != chart)
                    {
                        TargetSong.Charts.Remove(chart);
                        EditorUtility.SetDirty(TargetSong);
                        break;
                    }
                    GUILayout.EndHorizontal();
                }
                if (GUILayout.Button("Create New Chart"))
                {
                    Chart chart = new Chart();
                    TargetSong.Charts.Add(chart);
                    TargetChart = chart;
                    EditorUtility.SetDirty(TargetSong);
                }
                GUILayout.EndScrollView();
            }
            else if (TargetThing is Chart)
            {
                Chart thing = (Chart)TargetThing;
                
                GUI.Label(new Rect(7, 2, 226, 20), "Chart Details", "boldLabel");
                GUILayout.Space(8);
                scrollPos = GUILayout.BeginScrollView(scrollPos);
                GUILayout.Label("Difficulty", "boldLabel");
                thing.DifficultyIndex = EditorGUILayout.IntField("Index", thing.DifficultyIndex);
                thing.DifficultyName = EditorGUILayout.TextField("Name", thing.DifficultyName);
                thing.DifficultyLevel = EditorGUILayout.TextField("Level", thing.DifficultyLevel);
                thing.ChartConstant = EditorGUILayout.IntField("Constant", thing.ChartConstant);
                GUILayout.Space(8);
                GUILayout.Label("Layout", "boldLabel");
                thing.CameraPivot = EditorGUILayout.Vector3Field("Camera Pivot", thing.CameraPivot);
                thing.CameraRotation = EditorGUILayout.Vector3Field("Camera Rotation", thing.CameraRotation);
                GUILayout.Space(8);
                GUILayout.Label("Appearance", "boldLabel");
                thing.BackgroundColor = EditorGUILayout.ColorField("Background Color", thing.BackgroundColor);
                thing.InterfaceColor = EditorGUILayout.ColorField("Interface Color", thing.InterfaceColor);
                thing.LaneMaterial = (Material)EditorGUILayout.ObjectField("Lane Material", thing.LaneMaterial, typeof(Material), false);
                thing.HitMaterial = (Material)EditorGUILayout.ObjectField("Hit Material", thing.HitMaterial, typeof(Material), false);
                GUILayout.EndScrollView();
            }
            else if (TargetThing is Lane)
            {
                Lane thing = (Lane)TargetThing;
                
                GUI.Label(new Rect(7, 2, 226, 20), "Lane", "boldLabel");
                GUILayout.Space(8);
                scrollPos = GUILayout.BeginScrollView(scrollPos);

                GUIStyle labelStyle = new GUIStyle("label");
                labelStyle.padding = new RectOffset(3, 3, 1, 1);
                labelStyle.fontSize = 10;

                GUIStyle rightStyle = new GUIStyle(labelStyle);
                rightStyle.alignment = TextAnchor.UpperRight;
                rightStyle.normal.textColor = new Color(rightStyle.normal.textColor.r, 
                    rightStyle.normal.textColor.g, rightStyle.normal.textColor.b, .5f);

                GUIStyle fieldStyle = new GUIStyle("textField");
                fieldStyle.padding = new RectOffset(3, 3, 1, 1);
                fieldStyle.fontSize = 10;

                GUIStyle buttonStyle = new GUIStyle("button");
                buttonStyle.padding = new RectOffset(3, 3, 1, 1);
                buttonStyle.fontSize = 10;

                GUIStyle buttonLeftStyle = new GUIStyle(buttonStyle);
                buttonLeftStyle.alignment = TextAnchor.UpperLeft;

                GUIStyle bStyle = new GUIStyle(fieldStyle);
                bStyle.fontStyle = FontStyle.Bold;

                GUILayout.Label("Steps", "boldLabel");
                float h = 0;
                float o = GUILayoutUtility.GetLastRect().yMax;
                float a = thing.LaneSteps[0].Offset;

                List<string> est = new List<string>();
                List<string> eso = new List<string>();
                foreach (Ease ease in Ease.Eases) {
                    eso.Add(ease.ID);
                    est.Add(ease.Name);
                }

                foreach (LaneStep step in thing.LaneSteps)
                {
                    GUI.Label(new Rect(19, h + o + 2, 187, 48), "", "buttonMid");

                    step.Offset = EditorGUI.FloatField(new Rect(20, h + o + 4, 40, 14), step.Offset, bStyle);
                    GUI.Label(new Rect(20, h + o + 4, 40, 14), "b", rightStyle);
                    step.Speed = EditorGUI.FloatField(new Rect(61, h + o + 4, 40, 14), step.Speed, fieldStyle);
                    GUI.Label(new Rect(61, h + o + 4, 40, 14), "x", rightStyle);

                    {
                        step.StartPos.x = EditorGUI.FloatField(new Rect(20, h + o + 19, 40, 14), step.StartPos.x, fieldStyle);
                    GUI.Label(new Rect(20, h + o + 19, 40, 14), "x0", rightStyle);
                        int easeX = eso.IndexOf(step.StartEaseX);
                        step.StartEaseXMode = (EaseMode)EditorGUI.EnumPopup(new Rect(61, h + o + 19, 17, 14), step.StartEaseXMode, buttonStyle);
                        GUI.Button(new Rect(61, h + o + 19, 17, 14), new [] { "I", "O", "IO" }[(int)step.StartEaseXMode], buttonStyle );
                        int newEaseX = EditorGUI.Popup(new Rect(79, h + o + 19, 30, 14), easeX, est.ToArray(), buttonLeftStyle);
                        if (newEaseX != easeX) step.StartEaseX = eso[newEaseX];
                        
                        step.StartPos.y = EditorGUI.FloatField(new Rect(110, h + o + 19, 40, 14), step.StartPos.y, fieldStyle);
                    GUI.Label(new Rect(110, h + o + 19, 40, 14), "y0", rightStyle);
                        int easeY = eso.IndexOf(step.StartEaseY);
                        step.StartEaseYMode = (EaseMode)EditorGUI.EnumPopup(new Rect(151, h + o + 19, 17, 14), step.StartEaseYMode, buttonStyle);
                        GUI.Button(new Rect(151, h + o + 19, 17, 14), new [] { "I", "O", "IO" }[(int)step.StartEaseYMode], buttonStyle );
                        int newEaseY = EditorGUI.Popup(new Rect(169, h + o + 19, 30, 14), easeY, est.ToArray(), buttonLeftStyle);
                        if (newEaseY != easeY) step.StartEaseY = eso[newEaseY];
                    }
                    {
                        step.EndPos.x = EditorGUI.FloatField(new Rect(20, h + o + 34, 40, 14), step.EndPos.x, fieldStyle);
                    GUI.Label(new Rect(20, h + o + 34, 40, 14), "x1", rightStyle);
                        int easeX = eso.IndexOf(step.EndEaseX);
                        step.EndEaseXMode = (EaseMode)EditorGUI.EnumPopup(new Rect(61, h + o + 34, 17, 14), step.EndEaseXMode, buttonStyle);
                        GUI.Button(new Rect(61, h + o + 34, 17, 14), new [] { "I", "O", "IO" }[(int)step.EndEaseXMode], buttonStyle );
                        int newEaseX = EditorGUI.Popup(new Rect(79, h + o + 34, 30, 14), easeX, est.ToArray(), buttonLeftStyle);
                        if (newEaseX != easeX) step.EndEaseX = eso[newEaseX];
                        
                        step.EndPos.y = EditorGUI.FloatField(new Rect(110, h + o + 34, 40, 14), step.EndPos.y, fieldStyle);
                    GUI.Label(new Rect(110, h + o + 34, 40, 14), "y1", rightStyle);
                        int easeY = eso.IndexOf(step.EndEaseY);
                        step.EndEaseYMode = (EaseMode)EditorGUI.EnumPopup(new Rect(151, h + o + 34, 17, 14), step.EndEaseYMode, buttonStyle);
                        GUI.Button(new Rect(151, h + o + 34, 17, 14), new [] { "I", "O", "IO" }[(int)step.EndEaseYMode], buttonStyle );
                        int newEaseY = EditorGUI.Popup(new Rect(169, h + o + 34, 30, 14), easeY, est.ToArray(), buttonLeftStyle);
                        if (newEaseY != easeY) step.EndEaseY = eso[newEaseY];
                    }

                    if (GUI.Button(new Rect(3, h + o + 2, 16, 48), "⋮", "buttonLeft"))
                    {
                        TargetThing = step;
                    }
                    if (GUI.Button(new Rect(202, h + o + 2, 16, 48), "x", "buttonRight") && thing.LaneSteps.Count > 1)
                    {
                        thing.LaneSteps.Remove(step);
                        break;
                    }
                    h += 50;
                }
                GUILayout.Space(h);
                if (GUILayout.Button("Create New Step"))
                {
                    LaneStep step = new LaneStep();
                    step.Offset = Mathf.Round(pos * 1000) / 1000;
                    step.StartPos = thing.LaneSteps[thing.LaneSteps.Count - 1].StartPos;
                    step.EndPos = thing.LaneSteps[thing.LaneSteps.Count - 1].EndPos;
                    thing.LaneSteps.Add(step);
                    thing.LaneSteps.Sort((x, y) => x.Offset.CompareTo(y.Offset));
                }
                GUILayout.EndScrollView();
                
                if (thing.LaneSteps[0].Offset != a) 
                {
                    TargetChart.Lanes.Sort((x, y) => x.LaneSteps[0].Offset.CompareTo(y.LaneSteps[0].Offset));
                }
            }
            else if (TargetThing is HitObject)
            {
                HitObject thing = (HitObject)TargetThing;

                GUIStyle rightStyle = new GUIStyle("label");
                rightStyle.alignment = TextAnchor.UpperRight;
                rightStyle.normal.textColor = new Color(rightStyle.normal.textColor.r, 
                    rightStyle.normal.textColor.g, rightStyle.normal.textColor.b, .5f);
                
                GUI.Label(new Rect(7, 2, 226, 20), "Hit Object", "boldLabel");
                thing.Offset = EditorGUI.FloatField(new Rect(163, 2, 75, 20), thing.Offset);
                GUI.Label(new Rect(163, 2, 75, 20), "b", rightStyle);
                GUILayout.Space(8);
                scrollPos = GUILayout.BeginScrollView(scrollPos);

                GUILayout.Label("Transform", "boldLabel");
                thing.Position = EditorGUILayout.FloatField("Position", thing.Position);
                thing.Length = EditorGUILayout.FloatField("Length", thing.Length);

                float start, end;
                float startR = start = thing.Position;
                float endR = end = thing.Position + thing.Length;
                EditorGUILayout.MinMaxSlider(ref start, ref end, 0, 1);
                if (startR != start || endR != end) 
                {
                    thing.Length = Mathf.Round((end - start) / .05f) * .05f;
                    thing.Position = Mathf.Round(start / .05f) * .05f;
                }

                GUILayout.EndScrollView();
            }
        }
        else if (inspectMode == "storyboard") 
        {
            GUI.Label(new Rect(7, 2, 226, 20), "Storyboard", "boldLabel");
            GUILayout.Space(8);
            if (TargetThing is IStoryboardable)
            {
                IStoryboardable thing = (IStoryboardable)TargetThing;
                Storyboard sb = thing.Storyboard;

                GUIStyle labelStyle = new GUIStyle("label");
                labelStyle.padding = new RectOffset(3, 3, 1, 1);
                labelStyle.fontSize = 10;

                GUIStyle rightStyle = new GUIStyle(labelStyle);
                rightStyle.alignment = TextAnchor.UpperRight;
                rightStyle.normal.textColor = new Color(rightStyle.normal.textColor.r, 
                    rightStyle.normal.textColor.g, rightStyle.normal.textColor.b, .5f);

                GUIStyle fieldStyle = new GUIStyle("textField");
                fieldStyle.padding = new RectOffset(3, 3, 1, 1);
                fieldStyle.fontSize = 10;

                GUIStyle buttonStyle = new GUIStyle("button");
                buttonStyle.padding = new RectOffset(3, 3, 1, 1);
                buttonStyle.fontSize = 10;

                GUIStyle buttonLeftStyle = new GUIStyle(buttonStyle);
                buttonLeftStyle.alignment = TextAnchor.UpperLeft;

                GUIStyle bStyle = new GUIStyle(fieldStyle);
                bStyle.fontStyle = FontStyle.Bold;

                List<string> tst = new List<string>();
                List<string> tso = new List<string>();
                foreach (TimestampType type in (TimestampType[])thing.GetType().GetField("TimestampTypes").GetValue(null)) {
                    tso.Add(type.ID);
                    tst.Add(type.Name);
                }

                List<string> est = new List<string>();
                List<string> eso = new List<string>();
                foreach (Ease ease in Ease.Eases) {
                    eso.Add(ease.ID);
                    est.Add(ease.Name);
                }


                int add = EditorGUI.Popup(new Rect(218, 2, 20, 20), -1, tst.ToArray(), "button");
                if (add != -1) {
                    sb.Timestamps.Add(new Timestamp {
                        ID = tso[add],
                        Time = pos,
                    });
                }
                GUI.Button(new Rect(218, 2, 20, 20), "+");

                scrollPos = GUILayout.BeginScrollView(scrollPos);
                
                float h = 0;
                float o = 0; // GUILayoutUtility.GetLastRect().yMax;

                foreach (Timestamp ts in sb.Timestamps)
                {
                    GUI.Label(new Rect(3, h + o + 2, 203, 33), "", "buttonLeft");

                    ts.Time = EditorGUI.FloatField(new Rect(5, h + o + 4, 40, 14), ts.Time, bStyle);
                    GUI.Label(new Rect(5, h + o + 4, 40, 14), "b", rightStyle);
                    GUI.Label(new Rect(45, h + o + 4, 30, 14), "time", labelStyle);

                    ts.Duration = EditorGUI.FloatField(new Rect(5, h + o + 19, 40, 14), ts.Duration, bStyle);
                    GUI.Label(new Rect(5, h + o + 19, 40, 14), "b", rightStyle);
                    GUI.Label(new Rect(45, h + o + 19, 30, 14), "dur", labelStyle);

                    int type = tso.IndexOf(ts.ID);
                    int newType = EditorGUI.Popup(new Rect(116, h + o + 4, 83, 14), type, tst.ToArray(), buttonLeftStyle);
                    if (newType != type) ts.ID = tso[newType];

                    ts.Target = EditorGUI.FloatField(new Rect(75, h + o + 4, 40, 14), ts.Target, bStyle);

                    ts.EaseMode = (EaseMode)EditorGUI.EnumPopup(new Rect(75, h + o + 19, 40, 14), ts.EaseMode, buttonStyle);

                    int ease = eso.IndexOf(ts.Easing);
                    int newEase = EditorGUI.Popup(new Rect(116, h + o + 19, 83, 14), ease, est.ToArray(), buttonLeftStyle);
                    if (newEase != ease) ts.Easing = eso[newEase];

                    if (GUI.Button(new Rect(202, h + o + 2, 16, 33), "x", "buttonRight"))
                    {
                        sb.Timestamps.Remove(ts);
                        break;
                    }
                    h += 35;
                }
                
                GUILayout.Space(h);
                GUILayout.EndScrollView();
            }
            else
            {
                GUILayout.Label("This object is not storyboardable.");
            }
        }
    }

    #endregion

    /////////////////////
    #region Picker Window
    /////////////////////

    public string pickermode = "select";

    public void Picker(int id) {
        if (GUI.Toggle(new Rect(0, 0, 33, 33), pickermode == "select", new GUIContent("SEL", "Select"), "button")) pickermode = "select";
        if (GUI.Toggle(new Rect(0, 32, 33, 33), pickermode == "delete", new GUIContent("DEL", "Delete"), "button")) pickermode = "delete";

        if (timelineMode == "lane") 
        {
            if (GUI.Toggle(new Rect(0, 74, 33, 33), pickermode == "lane", new GUIContent("LNE", "Lane"), "button")) pickermode = "lane";
        }
        else if (timelineMode == "hit") 
        {
            if (GUI.Toggle(new Rect(0, 74, 33, 33), pickermode == "hit_normal", new GUIContent("NOR", "Normal Hit"), "button")) pickermode = "hit_normal";
        }
    }

    #endregion
}