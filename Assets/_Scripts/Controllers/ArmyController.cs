using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using _Scripts.Core;
using _Scripts.Models;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Pool;

namespace _Scripts.Controllers
{
    public class ArmyController : MonoBehaviour
    {
            private FormationBase _formation;
            private ObjectPool<GameObject> _pool;
            [SerializeField] private bool usePool;
            
            [SerializeField] private Cat _catUnit;
            
            private readonly List<GameObject> _spawnedUnits = new List<GameObject>();
            private List<Vector3> _points = new List<Vector3>();
            private Transform _parent;

            // Per-unit move-speed multiplier (±~18%) so the crowd doesn't slide into grid
            // slots at one mechanical uniform speed — same "small independent variance per
            // unit" pattern Cat.RandomizeIdle() already established.
            private readonly Dictionary<GameObject, float> _unitSpeed = new Dictionary<GameObject, float>();

            // Set whenever a unit starts its death beat. Read by the editor-side
            // GameplayCapture to snap a screenshot mid-fall so the loss moment is verifiable
            // in pixels, not just in code review.
            public static double LastKillRealtime;
            
            public GameObject boss;
       
            public float startZ;
            public float endZ;
            public float totalZ;


            private void Start()
            {
                CreateObjectPool();
                
                startZ = this.transform.position.z;
                endZ = boss.transform.position.z;

                totalZ = endZ - startZ;
            }
            
            /// <summary>
            ///  Arrays hold and create enough space for given parameters in memory.
            /// Resizing arrays can be expensive it uses more cpu cycle.
            /// If you will have 20 you should set it to 20
            /// </summary>
            /// 
            private void CreateObjectPool()
            {
                _pool = new ObjectPool<GameObject>(() => CreateCatUnit().gameObject, 
                    OnTakeFromPool, 
                    OnReturnToPool, 
                    OnDestroyObject, 
                    false, 
                    100, 
                    200);
            }

            private Cat CreateCatUnit()
            {
                Cat go = Instantiate(_catUnit);
                go.ControlAnimationState(1);
                return go;
            }

            private void OnTakeFromPool(GameObject player)
            {
                player.SetActive(true);
                player.GetComponent<Cat>().ControlAnimationState(1);
            }

            private void OnReturnToPool(GameObject player)
            {
                player.SetActive(false);
            }

            private void OnDestroyObject(GameObject player)
            {
                Destroy(player);
            }
            

            private FormationBase Formation {
                get {
                    if (_formation == null) _formation = GetComponent<FormationBase>();
                    return _formation;
                }
                set => _formation = value;

            }
        
            private void Awake()
            {
                _parent = gameObject.transform;
            }
        
            private void FixedUpdate() {
                 SetFormation();
                 ProgressBar();
            }
            
            private void ProgressBar()
            {
                // UIManager.Instance is assigned in Awake (StaticInstance base). If the Canvas /
                // UIManager object is inactive or missing from the scene it stays null forever,
                // and this line — called every FixedUpdate — spams NullReferenceException at
                // ~50/sec, which tanks the frame rate and buries every other Console message
                // (observed 2026-07-20: ~8k exceptions). The progress bar is cosmetic; it must
                // never be able to take down the run. Guard rather than assume the HUD is wired.
                if (UIManager.Instance == null) return;

                float currentZ = this.transform.position.z;
                float diffZ = currentZ - startZ;
                float process = diffZ / totalZ;
                UIManager.Instance.UpdateProcess(process);
            }
        
            private void SetFormation() {
                _points = Formation.EvaluatePoints().ToList();
        
                if (_points.Count > _spawnedUnits.Count) {
                    var remainingPoints = _points.Skip(_spawnedUnits.Count);
                    Spawn(remainingPoints);
                }
                else if (_points.Count < _spawnedUnits.Count) {
                    Kill(_spawnedUnits.Count - _points.Count);
                }
                for (var i = 0; i < _spawnedUnits.Count; i++) {
                    float speed = _unitSpeed.TryGetValue(_spawnedUnits[i], out var s) ? s : 3F;
                    _spawnedUnits[i].transform.position = Vector3.MoveTowards(_spawnedUnits[i].transform.position, transform.position + _points[i], speed * Time.deltaTime);
                }
            }
        
            private void Spawn(IEnumerable<Vector3> points) {
                
                foreach (Vector3 pos in points)
                {
                     GameObject unit = usePool ?  _pool.Get(): Instantiate(_catUnit.gameObject);
                     SetUnitTransform(unit,pos);
                }
            }

            private void SetUnitTransform(GameObject go, Vector3 position)
            {
                go.transform.SetParent(_parent);
                go.transform.SetPositionAndRotation(transform.position + position, Quaternion.identity);
                _spawnedUnits.Add(go);
                _unitSpeed[go] = UnityEngine.Random.Range(2.5F, 3.6F);
            }

            public void KillGameObject(GameObject go)
            {
                _spawnedUnits.Remove(go);
                _unitSpeed.Remove(go);
                PlayDeathBeat(go);
            }

            // Kill() removes _spawnedUnits.First() — BoxFormation yields its z=0 row first,
            // which after centering is the most-NEGATIVE-z row, i.e. the REAR rank of a crowd
            // running toward +z. So the trailing edge visibly loses members, which is the
            // correct read (confirmed by reading the yield order, per the HOD dispatch ask).
            private void Kill(int num) {
                for (int i = 0; i < num; i++) {
                    GameObject unit = _spawnedUnits.First();
                    _spawnedUnits.Remove(unit);
                    _unitSpeed.Remove(unit);
                    PlayDeathBeat(unit);
                }
            }

            // A lost unit used to vanish in a single frame (Destroy/Release straight from
            // Kill) — the count changed but losing crowd FELT like nothing, which was Ali's
            // core 5/10 complaint. No death clip exists in the rig's Animator (only
            // startRunning/stopRunning are wired), so this is a code-only exit beat:
            // knock the unit up-and-backward, tumble, shrink, THEN destroy/release.
            private void PlayDeathBeat(GameObject unit)
            {
                LastKillRealtime = Time.realtimeSinceStartupAsDouble;

                unit.transform.SetParent(null);
                foreach (var col in unit.GetComponentsInChildren<Collider>())
                    col.enabled = false; // must not re-trigger gates/obstacles while falling
                var rb = unit.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true; // physics would fight the tween

                Vector3 knock = new Vector3(UnityEngine.Random.Range(-0.5F, 0.5F), 0.3F, -1.2F);
                Vector3 originalScale = unit.transform.localScale;
                var seq = DOTween.Sequence();
                seq.Join(unit.transform.DOMove(unit.transform.position + knock, 0.45F).SetEase(Ease.OutQuad));
                seq.Join(unit.transform.DOLocalRotate(new Vector3(-75F, UnityEngine.Random.Range(-50F, 50F), 0F), 0.45F, RotateMode.LocalAxisAdd));
                seq.Join(unit.transform.DOScale(originalScale * 0.05F, 0.45F).SetEase(Ease.InQuad));
                seq.OnComplete(() =>
                {
                    if (usePool)
                    {
                        // pool reuse must not inherit death-beat mutations
                        unit.transform.localScale = originalScale;
                        unit.transform.rotation = Quaternion.identity;
                        foreach (var col in unit.GetComponentsInChildren<Collider>())
                            col.enabled = true;
                        if (rb != null) rb.isKinematic = false;
                        _pool.Release(unit);
                    }
                    else Destroy(unit);
                });
            }
    }
}
