using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

[AddComponentMenu("Dog/DogBrain")]
public class DogBrain : MonoBehaviour
{
    // ========================================================================
    // Settings (Inspector)
    // ========================================================================
    [Header("Animator")]
    public Animator animator;
    [Tooltip("Additiveの尻尾レイヤ Index（例: 1）")]
    public int tailLayerIndex = 1;

    [Header("Action Catalog (Inspectorで編集可)")]
    public List<ActionDef> catalog = new List<ActionDef>();

    [Header("Tail Wag (Additive Layer)")]
    [Range(0f, 2f)] public float wagNoiseSpeed = 0.12f;
    [Range(0f, 1f)] public float wagBase = 0.35f;
    [Range(0f, 1f)] public float wagAmp = 0.45f;

    [Header("Walk / Run Movement")]
    [Tooltip("歩いているときの前進速度 (m/s)")]
    public float walkForwardSpeed = 0.6f;
    [Tooltip("走っているときの前進速度 (m/s)")]
    public float runForwardSpeed = 1.2f;

    [Header("Idle Look-Around (方向転換)")]
    [Tooltip("Idle(Breathe)中にランダムで方向を変える最小間隔（秒）")]
    public Vector2 lookAroundInterval = new Vector2(2.5f, 5.0f);
    [Tooltip("一度の方向転換の角度範囲（度）")]
    public Vector2 lookAroundAngle = new Vector2(-60f, 60f);
    [Tooltip("方向転換にかける時間（秒）")]
    public Vector2 lookAroundDuration = new Vector2(0.35f, 0.9f);

    [Header("Obstacle / Collision Settings")]
    public LayerMask obstacleMask;
    [Tooltip("レイの届く距離(m)")] public float probeLength = 1.2f;
    [Tooltip("スフィアキャスト半径")] public float probeRadius = 0.12f;
    [Tooltip("犬の“目”の高さ")] public float eyeHeight = 0.25f;
    [Tooltip("1秒に何度まで曲がるか")] public float maxTurnDegPerSec = 300f;

    [Header("Debug")]
    public bool debugAvoidance = true;
    public Color debugRayColor = Color.red;
    public Color debugHitColor = Color.yellow;


    // ========================================================================
    // LLM Planner Settings
    // ========================================================================
    [Header("LLM Planner")]
    public bool usePlanner = true;
    [Tooltip("プランナーHTTPエンドポイント（POST JSON）")]
    public string plannerUrl = "http://localhost:8000/pet/plan";
    [Tooltip("プラン更新頻度[Hz]（例: 0.5 = 2秒ごと）")]
    [Range(0.1f, 2f)] public float plannerHz = 0.5f;
    [Tooltip("HTTPタイムアウト(秒)")]
    public int httpTimeoutSec = 3;


    // ========================================================================
    // Internal State
    // ========================================================================
    DogAction lastAction = DogAction.Breathe;
    DogAction currentAction = DogAction.Breathe;
    bool isBusyRoutine = false;

    // ★ GameManager から値を取得するプロパティ
    // (GameManagerがない場合はデフォルト値を返す安全設計)
    public float GetHunger() => GameManager.Instance ? Mathf.Clamp01(GameManager.Instance.DogHunger) : 0.0f;
    public float GetEnergy() => GameManager.Instance ? Mathf.Clamp01(GameManager.Instance.DogEnergy) : 0.6f;

    // LLM関連内部変数
    float plannerLookYawDeg = 0f;
    Dictionary<DogAction, float> biasWeights = new Dictionary<DogAction, float>();
    Queue<(DogAction action, float minSec, float maxSec, string until, float speed)> planQueue
        = new Queue<(DogAction, float, float, string, float)>();
    string currentPlanId = "";
    float planExpireAt = 0f;
    float currentActionStartTime = 0f;

    // Animator Hashes
    static readonly int stBreathing   = Animator.StringToHash("Breathing");
    static readonly int stRunning     = Animator.StringToHash("Running");
    static readonly int stWalking01   = Animator.StringToHash("Walking01");
    static readonly int stWalking02   = Animator.StringToHash("Walking02");
    static readonly int stSitStart    = Animator.StringToHash("SittingStart2");
    static readonly int stSitCycle    = Animator.StringToHash("SittingCycle2");
    static readonly int stSitEnd      = Animator.StringToHash("SittingEnd2");
    static readonly int stAngryStart  = Animator.StringToHash("AngryStart2");
    static readonly int stAngryCycle  = Animator.StringToHash("AngryCycle2");
    static readonly int stAngryEnd    = Animator.StringToHash("AngryEnd2");

    // Look / Turn logic
    float nextLookTime = 0f;
    bool isTurning = false;
    bool isForcedTurning = false;
    float savedAnimatorSpeed = 1f;

    DogAction lastPlayedAction = DogAction.Breathe;
    float lastPlayedDuration = 0f;


    // ========================================================================
    // DTO Classes (JSON)
    // ========================================================================
    [System.Serializable] public class WeightEntry { public string behavior; public float bias; }
    [System.Serializable] public class PlanStepDto {
        public string behavior;
        public float min_sec = 0.5f;
        public float max_sec = 2.0f;
        public string until;
        public float speed = -1f;
    }
    [System.Serializable] public class PlanResponse {
        public string plan_id;
        public float  expires_in_sec = 30f;
        public WeightEntry[] weights;
        public PlanStepDto[] sequence;
        public string notes;
        public float look_yaw_deg = 0f;
    }
    [System.Serializable] class PlannerRequest {
        public float hunger;
        public float energy;
        public string last_action;
        public string current_action;
        public float front_obstacle_dist;
        public float left_obstacle_dist;
        public float right_obstacle_dist;
        public bool  front_blocked;
    }


    // ========================================================================
    // Unity Lifecycle
    // ========================================================================
    void Reset()
    {
        if (catalog == null || catalog.Count == 0)
        {
            catalog = new List<ActionDef>
            {
                new ActionDef{ action=DogAction.Breathe,      weight=8.0f,  minDur=3f,   maxDur=5f,   cooldown=0.4f },
                new ActionDef{ action=DogAction.Walk1,        weight=1.0f,  minDur=2f,   maxDur=4f,   cooldown=1.0f },
                new ActionDef{ action=DogAction.Walk2,        weight=1.0f,  minDur=2f,   maxDur=4f,   cooldown=1.0f },
                new ActionDef{ action=DogAction.Run,          weight=0.25f, minDur=1.0f, maxDur=2.2f, cooldown=6.0f },
                new ActionDef{ action=DogAction.SitRoutine,   weight=0.9f,  minDur=2f,   maxDur=4f,   cooldown=3.0f },
                new ActionDef{ action=DogAction.AngryRoutine, weight=0.20f, minDur=0.6f, maxDur=1.2f, cooldown=7.0f  },
            };
        }
    }

    void Start()
    {
        if (!animator) animator = GetComponent<Animator>();

        if (tailLayerIndex >= 1 && tailLayerIndex < animator.layerCount)
            animator.SetLayerWeight(tailLayerIndex, 1f);

        ScheduleNextLook();
        StartCoroutine(Loop());

        if (usePlanner) StartCoroutine(PlannerPollingLoop());
    }

    void Update()
    {
        // Idle Look-Around
        if (currentAction == DogAction.Breathe && !isBusyRoutine && !isForcedTurning)
        {
            TryRandomLookAround();
        }

        if (isForcedTurning) return;

        // 前進移動
        if (currentAction == DogAction.Walk1 || currentAction == DogAction.Walk2)
        {
            transform.Translate(Vector3.forward * walkForwardSpeed * Time.deltaTime, Space.Self);
        }
        else if (currentAction == DogAction.Run)
        {
            transform.Translate(Vector3.forward * runForwardSpeed * Time.deltaTime, Space.Self);
        }
    }


    // ========================================================================
    // Main Behavior Loop
    // ========================================================================
    IEnumerator Loop()
    {
        while (true)
        {
            if (isBusyRoutine) { yield return null; continue; }

            var chosen = ChooseNext();
            float dur = Random.Range(chosen.minDur, chosen.maxDur);

            yield return Play(chosen, dur);

            chosen.nextOkTime = Time.time + chosen.cooldown;
            lastAction = lastPlayedAction;

            // ★ 感情値(GameManager)の更新
            ApplyEmotionDrift(lastPlayedAction, lastPlayedDuration);
        }
    }

    ActionDef ChooseNext()
    {
        float now = Time.time;
        var candidates = catalog.Where(a => now >= a.nextOkTime).ToList();
        if (candidates.Count == 0) return catalog[0];

        var weighted = new List<(ActionDef def, float w)>();
        float currentEnergy = GetEnergy(); // GameManagerから取得

        foreach (var a in candidates)
        {
            float w = Mathf.Max(0.0001f, a.weight);

            // ルールベースの重み付け
            if (lastAction == DogAction.Run &&
               (a.action == DogAction.SitRoutine || a.action == DogAction.Breathe)) w *= 1.6f;

            if (a.action == DogAction.Run)          w *= Mathf.Lerp(0.2f, 1.8f, currentEnergy);
            if (a.action == DogAction.AngryRoutine) w *= 0.4f;

            if ((a.action == DogAction.Walk1 || a.action == DogAction.Walk2) &&
                (lastAction == DogAction.Walk1 || lastAction == DogAction.Walk2)) w *= 0.8f;

            // LLMからのバイアス
            if (biasWeights != null && biasWeights.TryGetValue(a.action, out var bias))
            {
                float scale = Mathf.Max(0.1f, 1f + bias);
                w *= scale;
            }

            weighted.Add((a, w));
        }

        float total = weighted.Sum(x => x.w);
        float r = Random.Range(0f, total);
        foreach (var x in weighted)
        {
            if ((r -= x.w) <= 0f) return x.def;
        }
        return weighted[weighted.Count - 1].def;
    }

    IEnumerator Play(ActionDef def, float dur)
    {
        // LLMシーケンスがある場合はそちらを優先
        if (usePlanner && planQueue.Count > 0 && Time.time <= planExpireAt)
        {
            var (pa, minS, maxS, until, speed) = planQueue.Peek();
            def = catalog.FirstOrDefault(c => c.action == pa) ?? def;
            dur = Mathf.Clamp(Random.Range(minS, maxS), 0.2f, def.maxDur);
            planQueue.Dequeue();
        }

        DogAction playedAction = def.action;
        float playedDur = dur;

        currentAction = def.action;
        currentActionStartTime = Time.time;

        switch (def.action)
        {
            case DogAction.Breathe:
                CrossFade(stBreathing, 0.18f);
                yield return new WaitForSeconds(dur);
                break;

            case DogAction.Walk1:
                CrossFade(stWalking01, 0.20f);
                yield return new WaitForSeconds(dur);
                break;

            case DogAction.Walk2:
                CrossFade(stWalking02, 0.20f);
                yield return new WaitForSeconds(dur);
                break;

            case DogAction.Run:
                CrossFade(stRunning, 0.15f);
                yield return new WaitForSeconds(dur);
                break;

            case DogAction.SitRoutine:
                yield return StartCoroutine(PlayRoutine(stSitStart, stSitCycle, stSitEnd, dur));
                break;

            case DogAction.AngryRoutine:
                yield return StartCoroutine(PlayRoutine(stAngryStart, stAngryCycle, stAngryEnd, Mathf.Clamp(dur, 0.6f, 1.6f)));
                break;
            
            // EatRoutineは削除されたため、ここには到達しません
        }

        currentAction = DogAction.Breathe;
        lastPlayedAction = playedAction;
        lastPlayedDuration = playedDur;
    }

    IEnumerator PlayRoutine(int startHash, int cycleHash, int endHash, float cycleSeconds)
    {
        isBusyRoutine = true;

        CrossFade(startHash, 0.2f);
        yield return StartCoroutine(WaitStateFinish(startHash, 0.9f));
        
        CrossFade(cycleHash, 0.2f);
        yield return new WaitForSeconds(cycleSeconds);

        CrossFade(endHash, 0.2f);
        yield return StartCoroutine(WaitStateFinish(endHash, 0.9f));

        isBusyRoutine = false;
    }

    // ========================================================================
    // Helpers & Animation Logic
    // ========================================================================
    IEnumerator WaitStateFinish(int targetHash, float threshold = 0.95f, int layer = 0)
    {
        yield return null;
        float waitLimit = 1.0f;
        while (animator.GetCurrentAnimatorStateInfo(layer).shortNameHash != targetHash)
        {
            waitLimit -= Time.deltaTime;
            if (waitLimit <= 0f) break; 
            yield return null;
        }
        while (animator.GetCurrentAnimatorStateInfo(layer).shortNameHash == targetHash)
        {
            if (animator.GetCurrentAnimatorStateInfo(layer).normalizedTime >= threshold) break;
            yield return null;
        }
    }

    void ScheduleNextLook()
    {
        nextLookTime = Time.time + Random.Range(lookAroundInterval.x, lookAroundInterval.y);
    }

    void TryRandomLookAround()
    {
        if (isTurning || isForcedTurning) return;
        if (Time.time < nextLookTime) return;

        float angle;
        // LLMからの指示があれば優先
        if (usePlanner && Mathf.Abs(plannerLookYawDeg) > 0.01f)
        {
            angle = Mathf.Clamp(plannerLookYawDeg, lookAroundAngle.x, lookAroundAngle.y);
            plannerLookYawDeg = 0f;
        }
        else
        {
            angle = Random.Range(lookAroundAngle.x, lookAroundAngle.y);
        }

        float duration = Random.Range(lookAroundDuration.x, lookAroundDuration.y);
        float safeAngle = FindSafeTurnAngle(angle);

        StartCoroutine(TurnBy(safeAngle, duration));
        ScheduleNextLook();
    }

    float FindSafeTurnAngle(float desiredAngle)
    {
        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Vector3 dir = Quaternion.Euler(0f, desiredAngle, 0f) * transform.forward;
        if (Physics.SphereCast(origin, probeRadius, dir, out var hit, probeLength, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            // ぶつかるなら反対を向く簡易ロジック
            return desiredAngle + 180f;
        }
        return desiredAngle;
    }

    IEnumerator TurnBy(float angleDeg, float duration)
    {
        isTurning = true;
        Quaternion from = transform.rotation;
        Quaternion to = Quaternion.Euler(0f, transform.eulerAngles.y + angleDeg, 0f);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, duration);
            transform.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        isTurning = false;
    }

    void CrossFade(int stateHash, float fade, bool normalizedRandom = false, int layer = 0)
    {
        if (isForcedTurning) return;
        float t = normalizedRandom ? Random.value : 0f;
        animator.CrossFade(stateHash, fade, layer, t);
    }

    // ★ GameManagerの値を更新するメソッド
    void ApplyEmotionDrift(DogAction a, float dur)
    {
        if (GameManager.Instance == null) return;

        float e = GameManager.Instance.DogEnergy;
        float h = GameManager.Instance.DogHunger;

        if (a == DogAction.Run) e -= 0.15f;
        else if (a == DogAction.Walk1 || a == DogAction.Walk2) e -= 0.05f;
        else if (a == DogAction.SitRoutine || a == DogAction.Breathe) e += 0.06f;

        h += 0.03f * dur;

        GameManager.Instance.DogEnergy = Mathf.Clamp01(e);
        GameManager.Instance.DogHunger = Mathf.Clamp01(h);
    }


    // ========================================================================
    // Collision / Obstacle Logic
    // ========================================================================
    void OnCollisionEnter(Collision collision)
    {
        if ((obstacleMask.value & (1 << collision.gameObject.layer)) == 0) return;
        if (isForcedTurning) return;
        if (currentAction != DogAction.Walk1 && currentAction != DogAction.Walk2 && currentAction != DogAction.Run) return;

        var contact = collision.contacts[0];
        StartCoroutine(ForcedTurnFromCollision(contact.normal));
    }

    IEnumerator ForcedTurnFromCollision(Vector3 wallNormal)
    {
        isForcedTurning = true;
        savedAnimatorSpeed = animator.speed;
        animator.speed = 0f;

        Vector3 forward = transform.forward;
        Vector3 along = Vector3.ProjectOnPlane(forward, wallNormal);
        if (along.sqrMagnitude < 0.0001f) along = Vector3.Cross(wallNormal, Vector3.up);
        along.Normalize();

        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        Vector3[] candidates = new[] { along, -along };
        Vector3 bestDir = along;
        float bestScore = -1f;

        foreach (var dir in candidates)
        {
            float score;
            if (Physics.SphereCast(origin, probeRadius, dir, out var hit, probeLength, obstacleMask, QueryTriggerInteraction.Ignore))
                score = hit.distance;
            else
            {
                bestDir = dir;
                bestScore = probeLength + 1f;
                break;
            }
            if (score > bestScore) { bestScore = score; bestDir = dir; }
        }

        Quaternion targetRot = Quaternion.LookRotation(bestDir, Vector3.up);
        while (true)
        {
            float step = maxTurnDegPerSec * Time.deltaTime;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, step);
            if (Quaternion.Angle(transform.rotation, targetRot) < 0.5f) break;
            yield return null;
        }

        animator.speed = savedAnimatorSpeed;
        isForcedTurning = false;
    }

    float MeasureObstacleDistance(Vector3 dir)
    {
        Vector3 origin = transform.position + Vector3.up * eyeHeight;
        if (Physics.SphereCast(origin, probeRadius, dir, out var hit, probeLength, obstacleMask, QueryTriggerInteraction.Ignore))
            return hit.distance;
        return -1f;
    }


    // ========================================================================
    // LLM Parsing & Polling
    // ========================================================================
    DogAction ParseAction(string name)
    {
        if (string.IsNullOrEmpty(name)) return DogAction.Breathe;
        
        if (name.ToLowerInvariant().Contains("eat")) return DogAction.Breathe;

        if (System.Enum.TryParse<DogAction>(name, true, out var a)) return a;

        switch (name.ToLowerInvariant())
        {
            case "walk": case "walk1": return DogAction.Walk1;
            case "walk2": return DogAction.Walk2;
            case "run":   return DogAction.Run;
            case "sit": case "sitroutine": return DogAction.SitRoutine;
            case "angry": case "angryroutine": return DogAction.AngryRoutine;
            case "breathe": default: return DogAction.Breathe;
        }
    }

    IEnumerator PlannerPollingLoop()
    {
        var wait = new WaitForSeconds(1f / Mathf.Max(0.1f, plannerHz));
        while (true)
        {
            yield return wait;
            if (Time.time > planExpireAt)
            {
                planQueue.Clear();
                biasWeights.Clear();
                currentPlanId = "";
            }

            // 環境情報の取得
            Vector3 forward = transform.forward;
            Vector3 left = Quaternion.Euler(0, -45f, 0) * forward;
            Vector3 right = Quaternion.Euler(0, 45f, 0) * forward;

            float frontDist = MeasureObstacleDistance(forward);
            float leftDist  = MeasureObstacleDistance(left);
            float rightDist = MeasureObstacleDistance(right);
            bool blocked = frontDist > 0 && frontDist < 0.4f;

            // ★ GameManagerの値を使ってリクエスト作成
            var payload = new PlannerRequest() {
                hunger = GetHunger(),
                energy = GetEnergy(),
                last_action = lastAction.ToString(),
                current_action = currentAction.ToString(),
                front_obstacle_dist = frontDist,
                left_obstacle_dist  = leftDist,
                right_obstacle_dist = rightDist,
                front_blocked = blocked
            };

            string json = JsonUtility.ToJson(payload);

            using (var req = new UnityWebRequest(plannerUrl, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
                req.uploadHandler   = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = Mathf.Max(1, httpTimeoutSec);

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success) continue;

                var text = req.downloadHandler.text;
                if (string.IsNullOrEmpty(text)) continue;

                PlanResponse resp = null;
                try { resp = JsonUtility.FromJson<PlanResponse>(text); }
                catch { continue; }

                ApplyPlanResponse(resp);
            }
        }
    }

    void ApplyPlanResponse(PlanResponse resp)
    {
        if (resp == null) return;

        currentPlanId = string.IsNullOrEmpty(resp.plan_id) ? System.Guid.NewGuid().ToString() : resp.plan_id;
        planExpireAt  = Time.time + Mathf.Max(2f, resp.expires_in_sec);
        plannerLookYawDeg = resp.look_yaw_deg;

        biasWeights.Clear();
        if (resp.weights != null)
        {
            foreach (var w in resp.weights)
            {
                var act = ParseAction(w.behavior);
                if (act == DogAction.EatRoutine) continue;
                biasWeights[act] = w.bias;
            }
        }

        planQueue.Clear();
        if (resp.sequence != null)
        {
            foreach (var s in resp.sequence)
            {
                var act = ParseAction(s.behavior);
                if (act == DogAction.EatRoutine) continue;

                float minS = Mathf.Max(0.2f, s.min_sec);
                float maxS = Mathf.Max(minS, s.max_sec);
                planQueue.Enqueue((act, minS, maxS, s.until, s.speed));
            }
        }
    }
}