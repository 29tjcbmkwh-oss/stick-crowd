using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using _Scripts.Core;
using _Scripts.Models;
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
                    _spawnedUnits[i].transform.position = Vector3.MoveTowards(_spawnedUnits[i].transform.position, transform.position + _points[i], 3F * Time.deltaTime);
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
            }

            public void KillGameObject(GameObject go)
            {
                _spawnedUnits.Remove(go);
                Destroy(go);
            }
            private void Kill(int num) {
                for (int i = 0; i < num; i++) {
                    // GameObject unit = _spawnedUnits.LastOrDefault();
                    GameObject unit = _spawnedUnits.First();
                    _spawnedUnits.Remove(unit);
                    if (usePool)
                    {
                        _pool.Release(unit);
                    }
                    else
                    {
                        Destroy(unit);
                    }

                }
            }
    }
}
