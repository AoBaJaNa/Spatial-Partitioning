# Unity 공간 분할 탐색 최적화

Unity에서 **10,000개의 유닛을 대상으로 Brute Force → Uniform Grid → Dynamic Grid → Quadtree** 순서로 확장하며, 검색 비용뿐 아니라 **동적 인덱스 유지 비용까지 함께 비교**한 실험 프로젝트입니다.

단순히 특정 자료구조가 더 빠른지 확인하는 데서 끝내지 않고, **검색 반경 · 객체 분포 · 이동 비율 · Query 횟수**에 따라 어떤 구조가 유리한지 측정했습니다.

> 상세 실험 과정, 그래프, 시행착오는 [Notion 포트폴리오](https://app.notion.com/p/3d374e134c7980d5bfa4cdc405f93c71)에서 확인할 수 있습니다.

---

## 실험 흐름

1. **Brute Force** — 10,000개 전체 순회 기준 성능 측정
2. **Uniform Grid** — Cell Size에 따른 후보 수 / 조회 비용 비교
3. **Dynamic Grid** — 전체 Rebuild 대신 Cell 경계를 넘은 객체만 부분 갱신
4. **Quadtree** — 비균일 분포에서 적응형 공간 분할 비교
5. **Dynamic Quadtree** — Parent 탐색, 부분 재삽입, Split / Merge 적용
6. **Root Bounds 분석** — Root 이탈로 발생한 Full Rebuild 스파이크 분리
7. **Parameter Sweep** — MaxDepth / Leaf Capacity의 Trade-off 검증
8. **최종 비용 비교** — `Update + Query × Queries Per Frame` 기준 손익분기점 계산

---

## 핵심 결과

### 1. 좁은 검색에서는 Uniform Grid가 강했다

Radius **20m**, 10,000 Units 조건에서:

| 방식 | Checked | Query Avg |
| --- | ---: | ---: |
| Brute Force | 10,000 | 1.0523ms |
| Uniform Grid (Cell 10m) | 34 | **0.0047ms** |

후보 검사량을 약 **99.66% 감소**시켰습니다.

다만 Cell을 작게 만든다고 항상 빨라지지는 않았습니다. Radius 300m 실험에서는 Cell이 너무 작아지자 Dictionary 조회 횟수가 증가해 오히려 성능이 악화되었습니다.

> **작은 Cell** → 후보 수 감소 / Cell 조회 증가  
> **큰 Cell** → Cell 조회 감소 / 후보 수 증가

---

### 2. Dynamic Grid는 “움직인 수”보다 “Cell을 넘은 수”가 중요했다

객체가 이동할 때마다 전체 Grid를 다시 만드는 대신, 이전 Cell을 캐시하고 **Cell이 실제로 변경된 객체만 제거 / 삽입**하도록 수정했습니다.

Clustered 분포, Cell 10m 기준:

| Move | CellChanged / Frame | Dynamic Update Avg |
| ---: | ---: | ---: |
| 10% | 21.3 | 0.214ms |
| 50% | 105.9 | 1.203ms |
| 100% | 211.5 | 2.667ms |

10,000개가 모두 이동하더라도 매 프레임 실제 Cell을 넘는 객체는 일부뿐이었습니다.

Move 100% 기준 Full Rebuild와 비교하면:

| 방식 | Dynamic | Full Rebuild |
| --- | ---: | ---: |
| Uniform Grid | **2.667ms** | 3.819ms |
| Quadtree | **3.736ms** | 10.410ms |

특히 Quadtree는 전체 재구축보다 부분 갱신의 효과가 크게 나타났습니다.

---

## Quadtree를 추가한 이유

Uniform Grid는 모든 공간에 동일한 Cell Size를 사용합니다.

객체가 특정 지역에 몰려 있고 빈 공간이 넓은 경우, 하나의 Cell Size로 전체 공간을 최적화하기 어렵다고 판단해 **밀집 영역만 재귀적으로 세분화하는 Quadtree**를 구현했습니다.

Dynamic Quadtree에서는 각 객체가 현재 속한 Leaf를 역추적할 수 있도록

```csharp
Dictionary<GameObject, QuadtreeNode>
```

를 유지합니다.

Leaf를 벗어난 경우:

```text
기존 Leaf 제거
→ Parent를 따라 새 위치를 포함하는 조상 탐색
→ 해당 Node에서 재삽입
→ 기존 Branch Merge 검사
```

순서로 부분 갱신합니다.

---

## 시행착오 — Root Rebuild Spike

초기 Dynamic Quadtree에서는 간헐적으로 **12~15ms 수준의 Update Spike**가 발생했습니다.

Dynamic Frame과 Root Rebuild Frame을 따로 측정한 결과, 원인은 **객체가 Root Bounds 밖으로 이동할 때 발생하는 전체 Tree 재구축**이었습니다.

Root Margin을 충분히 확보한 뒤에는 Root Rebuild가 0회로 줄었고, Move 10% 조건의 최대 Update도 약 **15ms → 0.78ms** 수준으로 감소했습니다.

이 과정에서 단순 평균만 보는 대신 다음 지표를 추가했습니다.

- Root Rebuild 횟수 / 비용
- LeafChanged
- Split / Merge 횟수
- Dynamic Frame과 Rebuild Frame 분리 측정

---

## Quadtree Parameter Sweep

Radius 300m / Clustered / Move 100% 조건에서 `MaxDepth`와 `Leaf Capacity`를 조정했습니다.

대표 결과:

| MaxDepth / Capacity | Update Avg | Query Avg | Split | Merge |
| --- | ---: | ---: | ---: | ---: |
| 6 / 4 | 4.435ms | 0.336ms | 152 | 144 |
| **6 / 16** | 4.056ms | **0.333ms** | 39 | 0 |
| 6 / 32 | **3.819ms** | 0.351ms | 18 | 0 |
| 10 / 4 | 4.513ms | 0.397ms | 3357 | 3366 |

### 확인한 점

- `MaxDepth`를 무조건 키우면 좋아지지 않았습니다.
- 너무 깊게 분할하면 Leaf 경계 이탈과 Split / Merge가 급증했습니다.
- Capacity를 크게 잡으면 Update는 싸지지만 한 Leaf의 후보 수가 증가해 Query가 다시 느려졌습니다.
- 이번 workload에서는 **MaxDepth 6 / Capacity 16**을 Query와 Dynamic Update 사이의 대표 균형점으로 선택했습니다.

---

## 최종 비교 — Grid vs Quadtree

Radius **300m**, Clustered 분포에서는 Quadtree의 Query 비용이 더 낮게 측정됐습니다.

| Move | Grid Query | Quadtree Query |
| ---: | ---: | ---: |
| 10% | 0.463ms | **0.341ms** |
| 50% | 0.472ms | **0.349ms** |
| 100% | 0.484ms | **약 0.33~0.35ms** |

반대로 Dynamic Update 비용은 Uniform Grid가 더 낮았습니다.

따라서 한 프레임의 공간 분할 비용을 다음처럼 계산했습니다.

```text
Estimated Total Cost
= Update Cost + Query Cost × Queries Per Frame
```

Move 100% / Radius 300m 조건에서 측정 평균값을 기준으로 계산하면, 약 **8.7 Queries / Frame**부터 Quadtree의 누적 비용이 Uniform Grid보다 낮아졌습니다.

### 구조 선택 기준

| 상황 | 적합한 방식 |
| --- | --- |
| 좁은 반경 + 이동 많음 | **Uniform Grid** |
| 넓은 반경 + 비균일 분포 + Query 반복 | **Quadtree** |
| Query가 매우 적음 | **Brute Force도 합리적** |

핵심은 “더 고급 자료구조가 항상 빠르다”가 아니라, **검색 비용과 인덱스 유지 비용을 workload 기준으로 함께 봐야 한다**는 점이었습니다.

---

## Benchmark 신뢰성 보완

실험 중 측정 조건 자체도 여러 번 수정했습니다.

- 이동 대상을 앞쪽 N개가 아닌 **Fixed Seed Random Selection**으로 선택
- Dynamic / Full Rebuild의 **Timer 범위 통일**
- `Time.deltaTime` 대신 **고정 Simulation DeltaTime** 사용
- Warm-up 후 300 Update Frames 측정
- Query 500회 반복 측정
- Brute Force 결과와 비교하는 **Search Integrity 검증**
- 인덱스 중복 / 누락을 확인하는 **Index Integrity 검증**
- CSV 자동 저장으로 실험 결과 비교

모든 최종 비교 케이스에서 Search / Index Integrity가 PASS인지 확인했습니다.

---

## 핵심 코드

| 파일 | 역할 |
| --- | --- |
| [`SpatialTestManager.cs`](Assets/SpatialPartition/Scripts/SpatialTestManager.cs) | 실험 실행, Warm-up, Query / Update Benchmark, CSV, Integrity 검증 |
| [`UniformGridIndex.cs`](Assets/SpatialPartition/Scripts/Grid/UniformGridIndex.cs) | Cell Dictionary 구성 및 Dynamic Cell Update |
| [`QuadTreeIndex.cs`](Assets/SpatialPartition/Scripts/Grid/QuadTreeIndex.cs) | Quadtree Build, Parent 기반 재삽입, Split / Merge, Validation |
| [`PerformanceUI.cs`](Assets/SpatialPartition/Scripts/PerformanceUI.cs) | 검색 및 Benchmark 결과 HUD 표시 |
| [`MainUnit.cs`](Assets/SpatialPartition/Scripts/MainUnit.cs) | 검색 중심점 / Radius 제어 |

검색 구현은 `Assets/SpatialPartition/Scripts/SearchType`에, 이동 시뮬레이션은 `Assets/SpatialPartition/Scripts/Simulation`에 분리되어 있습니다.

---

## 실행 방법

1. Unity Hub에서 Unity `6000.3.10f1`로 프로젝트를 엽니다.
2. `Assets/SpatialPartition/Scenes/SpatialPartitionSandbox.unity`를 엽니다.
3. Play Mode를 실행합니다.
4. Inspector / HUD에서 Search Type, Radius, Cell Size, Move Percent 등을 변경해 비교합니다.
5. Benchmark 실행 시 동일 초기 상태와 Seed를 기준으로 측정 결과가 생성됩니다.

---

## 사용 기술

`Unity 6` · `C#` · `Uniform Grid` · `Quadtree` · `Dictionary` · `Gizmos` · `System.Diagnostics.Stopwatch` · `CSV Benchmark` · `Unity Profiler`
