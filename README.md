# SyncWorld2D Server

[클라이언트 프로젝트](https://github.com/floweryclover/syncworld-2d-client.git)

간단한 이동 동기화와 서버 측 엔티티 처리를 구현해본 프로젝트입니다.

* 추측 항법을 이용한 실시간 이동 동기화 (10ms 핑 가정, 이동 메시지 송수신 주기 100ms)
* 서버 측 충돌 처리(플레이어 <-> 축구공, 축구공 <-> 플랫폼)

### 동기화 및 상호작용 사진(4인 플레이어)
![4인동기화화면](https://blog.kakaocdn.net/dn/xp3cS/btsITiXYuES/zrQ2IpGonO3xV7NS1qcPrk/img.gif)
![공튀기기](https://blog.kakaocdn.net/dn/kyLqK/btsIUJUqVKn/3gakSIwtFcWmoGRUy1zki0/img.gif)