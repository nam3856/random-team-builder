# 🧠 27인 팀 랜덤 배정 시뮬레이터

이 프로젝트는 27명의 참가자를 대상으로 **4인 1조 팀을 무작위로 구성**(1팀은 3인)하되,  
**각 회차마다 같은 팀원이 절대 겹치지 않도록** 팀을 자동 생성해주는 Unity 기반 시뮬레이터입니다.

<br/>

## 🎯 핵심 규칙
- 총 인원: 28명
- 매 회차: 4인 1조 × 7팀
- **회차가 달라도 중복 없이** → 모든 팀원이 서로 "초면"이어야 함

<br/>

## 💻 기술 스택
- Unity (C#)
- DOTween (애니메이션 연출)
- FEEL (UI 및 조명 연출)
- TextMeshPro (UI)
- GitHub Desktop 기반 Git 관리

<br/>

## 🕹️ 기능 요약
- 1~3회차 랜덤 팀 생성
- 팀 별 파티클 연출 (게임 느낌!)
- 충돌되는 팀 조합은 자동 회피
- 팀 구성 결과를 시각적으로 확인 가능

<br/>

## 📁 사용 방법
1. `PreviousTeams.txt`에 이전 팀 입력
2. Unity에서 실행
3. 중복 방지 / 시드 선택
4. 버튼 클릭 시 팀 배정 연출 시작
5. 배정된 팀은 실행파일 폴더 경로의 `Result.csv`로 저장

<br/>

## 🔒 주의사항
> 해당 프로젝트는 테스트용이며, 공개된 레포지토리에는 유료 에셋이 포함되지 않습니다.  
> `.gitignore`를 통해 `Assets/Feel/`, `Assets/Retro Arsenal/` 등은 제외되었습니다.

---

## 🙋‍♂️ 만든 이유
프로젝트에서 **매 회차마다 새로운 조합으로 팀 빌딩**이 필요할 때  
반복적이고 피곤한 수작업을 자동화하기 위해 제작되었습니다.
~~핀볼 질려서 만들었습니다~~
