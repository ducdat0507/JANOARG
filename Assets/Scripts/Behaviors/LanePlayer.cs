using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LanePlayer : MonoBehaviour
{
    public Lane CurrentLane;
    public Transform Container;

    public List<MeshFilter> LaneMeshes;
    public List<float> Positions;
    public List<float> Times;
    public int Index;
    public float CurrentPos;
    public float CurrentTime;

    public void SetLane (Lane lane) 
    {
        foreach (Timestamp ts in lane.Storyboard.Timestamps)
        {
            ts.Duration = ChartPlayer.main.Song.Timing.ToSeconds(ts.Time + ts.Duration);
            ts.Time = ChartPlayer.main.Song.Timing.ToSeconds(ts.Time);
            ts.Duration -= ts.Time;
        }
        float sec = ChartPlayer.main.Song.Timing.ToSeconds(lane.LaneSteps[0].Offset);
        float pos = sec * lane.LaneSteps[0].Speed;
        Positions.Add(pos);
        Times.Add(sec);
        for (int a = 1; a < lane.LaneSteps.Count; a++) 
        {
            LaneStep prev = lane.LaneSteps[a - 1];
            LaneStep step = lane.LaneSteps[a];
            float nSec = ChartPlayer.main.Song.Timing.ToSeconds(step.Offset);
            float nPos = pos + prev.Speed * (nSec - sec);
            MeshFilter mf = Instantiate(ChartPlayer.main.LaneMeshSample, Container);
            mf.mesh = MakeLaneMesh(prev, step, pos * ChartPlayer.main.ScrollSpeed, nPos * ChartPlayer.main.ScrollSpeed, 0);
            mf.GetComponent<MeshRenderer>().material = ChartPlayer.main.CurrentChart.LaneMaterial;
            pos = nPos;
            sec = nSec;
            LaneMeshes.Add(mf);
            Positions.Add(pos);
            Times.Add(nSec);
        }
        CurrentLane = (Lane)lane;

        foreach (HitObject hit in lane.Objects)
        {
            
            HitPlayer hp = Instantiate(hit.Type == HitObject.HitType.Catch ? ChartPlayer.main.CatchHitSample : ChartPlayer.main.NormalHitSample, Container);
            hp.SetHit(this, hit);
        }
    }

    public void Update()
    {
        CurrentLane.Advance(ChartPlayer.main.CurrentTime);

        while (LaneMeshes.Count > 0) 
        {
            float t = Mathf.Min((ChartPlayer.main.CurrentTime - Times[Index]) / (Times[Index + 1] - Times[Index]), 1);
            if (t < 1)
            {
                CurrentPos = Mathf.LerpUnclamped(Positions[Index], Positions[Index + 1], t);
                if (t > 0) LaneMeshes[0].mesh = MakeLaneMesh(CurrentLane.LaneSteps[Index], CurrentLane.LaneSteps[Index + 1], Positions[Index] * ChartPlayer.main.ScrollSpeed, Positions[Index + 1] * ChartPlayer.main.ScrollSpeed, Mathf.Clamp01(t));
                Container.localPosition = Vector3.forward * CurrentPos * -ChartPlayer.main.ScrollSpeed;
                break;
            }
            else
            {
                Destroy(LaneMeshes[0].gameObject);
                LaneMeshes.RemoveAt(0);
                Index++;
            }
        }
    }

    public static Mesh MakeLaneMesh(LaneStep pre, LaneStep cur, float prePos, float curPos, float pos) 
    {
        Mesh mesh = new Mesh();

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

        if (cur.StartEaseX == "Linear" && cur.StartEaseY == "Linear" && cur.EndEaseX == "Linear" && cur.EndEaseY == "Linear")
        {
            AddStep(Vector3.Lerp((Vector3)pre.StartPos + Vector3.forward * prePos, (Vector3)cur.StartPos + Vector3.forward * curPos, pos), 
                Vector3.Lerp((Vector3)pre.EndPos + Vector3.forward * prePos, (Vector3)cur.EndPos + Vector3.forward * curPos, pos));
            AddStep((Vector3)cur.StartPos + Vector3.forward * curPos, (Vector3)cur.EndPos + Vector3.forward * curPos);
        }
        else
        {
            for (float x = pos; x <= 1; x = Mathf.Floor(x * 16 + 1.01f) / 16)
            {
                float cPos = Mathf.Lerp(prePos, curPos, x);
                Vector3 start = new Vector3(Mathf.Lerp(pre.StartPos.x, cur.StartPos.x, Ease.Get(x, cur.StartEaseX, cur.StartEaseXMode)),
                    Mathf.Lerp(pre.StartPos.y, cur.StartPos.y, Ease.Get(x, cur.StartEaseY, cur.StartEaseYMode)), cPos);
                Vector3 end = new Vector3(Mathf.Lerp(pre.EndPos.x, cur.EndPos.x, Ease.Get(x, cur.EndEaseX, cur.EndEaseXMode)),
                    Mathf.Lerp(pre.EndPos.y, cur.EndPos.y, Ease.Get(x, cur.EndEaseY, cur.EndEaseYMode)), cPos);
                AddStep(start, end);
            }
        }

        mesh.Clear();
        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();

        return mesh;
    }

    public static Mesh MakeHoldMesh(HitObject ho, LaneStep pre, LaneStep cur, float prePos, float curPos, float minPos, float maxPos) 
    {
        Mesh mesh = new Mesh();

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

        if (cur.StartEaseX == "Linear" && cur.StartEaseY == "Linear" && cur.EndEaseX == "Linear" && cur.EndEaseY == "Linear")
        {
            Vector3 start = Vector3.Lerp((Vector3)pre.StartPos + Vector3.forward * prePos, (Vector3)cur.StartPos + Vector3.forward * curPos, minPos);
            Vector3 end = Vector3.Lerp((Vector3)pre.EndPos + Vector3.forward * prePos, (Vector3)cur.EndPos + Vector3.forward * curPos, minPos);
            AddStep(Vector3.Lerp(start, end, ho.Position), Vector3.Lerp(start, end, ho.Position + ho.Length));
            Vector3 start2 = Vector3.Lerp((Vector3)pre.StartPos + Vector3.forward * prePos, (Vector3)cur.StartPos + Vector3.forward * curPos, maxPos);
            Vector3 end2 = Vector3.Lerp((Vector3)pre.EndPos + Vector3.forward * prePos, (Vector3)cur.EndPos + Vector3.forward * curPos, maxPos);
            AddStep(Vector3.Lerp(start2, end2, ho.Position), Vector3.Lerp(start2, end2, ho.Position + ho.Length));
        }
        else
        {
            void AddPos(float x)
            {
                float cPos = Mathf.Lerp(prePos, curPos, x);
                Vector3 start = new Vector3(Mathf.Lerp(pre.StartPos.x, cur.StartPos.x, Ease.Get(x, cur.StartEaseX, cur.StartEaseXMode)),
                    Mathf.Lerp(pre.StartPos.y, cur.StartPos.y, Ease.Get(x, cur.StartEaseY, cur.StartEaseYMode)), cPos);
                Vector3 end = new Vector3(Mathf.Lerp(pre.EndPos.x, cur.EndPos.x, Ease.Get(x, cur.EndEaseX, cur.EndEaseXMode)),
                    Mathf.Lerp(pre.EndPos.y, cur.EndPos.y, Ease.Get(x, cur.EndEaseY, cur.EndEaseYMode)), cPos);
                AddStep(Vector3.Lerp(start, end, ho.Position), Vector3.Lerp(start, end, ho.Position + ho.Length));
            }
            for (float x = minPos; x < maxPos; x = Mathf.Floor(x * 16 + 1.01f) / 16)
            {
                AddPos(x);
            }
            AddPos(maxPos);
        }

        mesh.Clear();
        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();

        return mesh;
    }

    public void GetPosition(float time, out Vector3 start, out Vector3 end)
    {
        int index = Times.FindIndex((x) => x > time);

        if (index == 0) {
            index = 1;
            float t = (time - Times[0]) / (Times[1] - Times[0]);
            float pos = Mathf.LerpUnclamped(Positions[0], Positions[1], t);
            start = (Vector3)CurrentLane.LaneSteps[0].StartPos + Vector3.forward * pos;
            end = (Vector3)CurrentLane.LaneSteps[0].EndPos + Vector3.forward * pos;
        }
        else if (index == -1)
        {
            index = Times.Count - 1;
            float t = (time - Times[index - 1]) / (Times[index] - Times[index - 1]);
            float pos = Mathf.LerpUnclamped(Positions[index - 1], Positions[index], t);
            start = (Vector3)CurrentLane.LaneSteps[index].StartPos + Vector3.forward * pos;
            end = (Vector3)CurrentLane.LaneSteps[index].EndPos + Vector3.forward * pos;
        }
        else 
        {
            float t = (time - Times[index - 1]) / (Times[index] - Times[index - 1]);
            float pos = Mathf.LerpUnclamped(Positions[index - 1], Positions[index], t);
            var pre = CurrentLane.LaneSteps[index - 1];
            var cur = CurrentLane.LaneSteps[index];
            if (cur.StartEaseX == "Linear" && cur.StartEaseY == "Linear" && cur.EndEaseX == "Linear" && cur.EndEaseY == "Linear")
            {
                start = Vector3.Lerp(pre.StartPos, cur.StartPos, t) + Vector3.forward * pos;
                end = Vector3.Lerp(pre.EndPos, cur.EndPos, t) + Vector3.forward * pos;
            }
            else 
            {
                start = new Vector3(Mathf.Lerp(pre.StartPos.x, cur.StartPos.x, Ease.Get(t, cur.StartEaseX, cur.StartEaseXMode)), 
                    Mathf.Lerp(pre.StartPos.y, cur.StartPos.y, Ease.Get(t, cur.StartEaseY, cur.StartEaseYMode)), pos);
                end = new Vector3(Mathf.Lerp(pre.EndPos.x, cur.EndPos.x, Ease.Get(t, cur.EndEaseX, cur.EndEaseXMode)), 
                    Mathf.Lerp(pre.EndPos.y, cur.EndPos.y, Ease.Get(t, cur.EndEaseY, cur.EndEaseYMode)), pos);
            }
        }
    }
}