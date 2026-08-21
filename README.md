# FAS I/O Connection Test

FASTECH Ezi-MOTION Plus-E 계열 Ethernet I/O 장치에 연결해 디지털 출력 두 개를 키보드로 제어하는 .NET 콘솔 테스트 프로그램입니다.
사용 제품은 Ezi-IO-EN-I8O8-E 입니다.
> dll 출처 : Ezi-IO Ethernet DIO package (https://fastech-motions.com/new/kor/sub0301.php)

## 주요 특징

- FASTECH `EziMOTIONPlusE.dll`을 P/Invoke로 호출해 Ethernet 장치에 연결
- `IO_PORT` 비트 마스크로 Output 1~16 개별 제어
- Space 키로 두 출력을 5초 간격으로 순차 ON/OFF
- N/M 키로 각 출력을 개별 토글
- ESC 종료 시 제어 대상 출력을 모두 OFF하고 연결 해제
- `IDisposable`을 이용한 연결 자원 정리
- 최대 16개 드라이버 인스턴스에 사용할 장치 번호 풀 제공
- 디지털 입력 폴링 예제 포함(현재 `Program.cs`에서 주석 처리됨)

## 현재 테스트 설정

| 항목 | 값 |
| --- | --- |
| 장치 IP | `192.168.0.2` |
| PORT1 | `IO_PORT.OUTPUT15` (`0x00004000`) |
| PORT2 | `IO_PORT.OUTPUT16` (`0x00008000`) |
| 순차 동작 지연 | 5초 |
| 대상 프레임워크 | .NET 10 (`net10.0`) |

> 현재 소스가 실제로 제어하는 포트는 Output 15/16입니다.

## 동작 방식

| 키 | 동작 |
| --- | --- |
| Space | OFF 상태에서는 PORT1 ON → 5초 대기 → PORT2 ON, ON 상태에서는 PORT2 OFF → 5초 대기 → PORT1 OFF |
| N | PORT1(Output 15) 상태 토글 |
| M | PORT2(Output 16) 상태 토글 |
| ESC | PORT1/PORT2를 OFF한 뒤 프로그램 종료 |

N과 M은 하나의 `currentOutputState` 변수를 공유합니다. 따라서 한 포트만 개별 토글한 뒤 다른 포트를 토글하면, 두 포트의 실제 상태와 내부 토글 상태가 서로 다를 수 있습니다. 정확한 개별 상태 추적이 필요하면 포트별 상태 변수를 사용하거나 `GetDigitalOutputState()`로 장치 상태를 읽어 처리하세요.

## 요구 사항

- Windows
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- FASTECH Ezi-MOTION Plus-E 계열 Ethernet I/O 장치
- FASTECH 공식 SDK의 `EziMOTIONPlusE.dll`
- PC와 장치가 통신 가능한 동일 네트워크 대역

`EziMOTIONPlusE.dll`은 제조사 SDK 파일이므로 이 저장소에 포함하지 않습니다. 실행 프로세스와 DLL의 아키텍처(x64/x86)가 일치해야 합니다.

## 실행 준비

1. 저장소를 복제합니다.

   ```powershell
   git clone https://github.com/kimAssembly/FAS_io_Connection_test.git
   cd FAS_io_Connection_test
   ```

2. 장치와 PC의 Ethernet을 연결하고 PC 네트워크를 장치와 같은 대역으로 설정합니다. 기본 소스는 `192.168.0.2`에 연결합니다.

3. 필요하면 `FAS_io_Connection_test/Program.cs`에서 장치 IP, 출력 포트, 지연 시간을 수정합니다.

   ```csharp
   const uint TARGET_OUTPUT_PORT1 = (uint)IO_PORT.OUTPUT15;
   const uint TARGET_OUTPUT_PORT2 = (uint)IO_PORT.OUTPUT16;

   if (!ioDriver.Ez_Connect("192.168.0.2"))
   {
       // 연결 실패 처리
   }
   ```

4. 프로젝트를 빌드합니다.

   ```powershell
   dotnet build .\FAS_io_Connection_test.slnx
   ```

5. FASTECH SDK에서 받은 `EziMOTIONPlusE.dll`을 실행 파일과 같은 폴더에 복사합니다.

   ```powershell
   Copy-Item '<FASTECH-SDK-경로>\EziMOTIONPlusE.dll' `
     '.\FAS_io_Connection_test\bin\Debug\net10.0\EziMOTIONPlusE.dll'
   ```

6. 실행합니다.

   ```powershell
   dotnet run --project .\FAS_io_Connection_test\FAS_io_Connection_test.csproj --no-build
   ```

## 프로젝트 구조

```text
FAS_io_Connection_test/
├─ FAS_io_Connection_test.slnx
├─ FAS_io_Connection_test/
│  ├─ Program.cs                    # 연결 및 키보드 기반 I/O 테스트
│  ├─ FAS_io_Connection_test.csproj
│  └─ EzDrv/
│     ├─ EzServo_Driver.cs          # 연결, 디지털 I/O, 장치 번호 관리 래퍼
│     ├─ LIB_EziMOTIONPlusE.cs      # 네이티브 DLL P/Invoke 선언
│     └─ MOTION_DEFINE_PlusE.cs     # FASTECH 상수·열거형·구조체 정의
└─ README.md
```

## 드라이버 API 요약

| API | 설명 |
| --- | --- |
| `Ez_Connect(ip, isbLog)` | 장치 번호를 할당하고 지정한 IP의 장치에 연결 |
| `Ez_Close()` | 연결을 닫고 장치 번호를 풀에 반환 |
| `GetDigitalInput()` | 전체 디지털 입력을 `uint` 비트 마스크로 반환 |
| `SetSingleOutput(portBit, isOn)` | 지정한 출력 비트만 ON/OFF |
| `SetDigitalOutput(data)` | 전체 출력 상태를 한 번에 설정 |
| `GetDigitalOutputState()` | 현재 전체 출력 상태 조회 |
| `CombinePorts(...)` | 여러 `IO_PORT` 값을 하나의 비트 마스크로 결합 |
| `MapValueToPorts(...)` | 숫자의 각 비트를 연속 출력 포트에 매핑 |
| `SetOutput(value)` | 정수 하위 16비트를 Output 1~16에 일괄 출력 |

비트 마스크 예시:

```csharp
uint mask = EzServo_Driver.CombinePorts(
    IO_PORT.OUTPUT01,
    IO_PORT.OUTPUT03,
    IO_PORT.OUTPUT08);

ioDriver.SetDigitalOutput(mask);
```

## 입력 모니터링 예제

`Program.cs`에는 `GetDigitalInput()`을 50ms 간격으로 폴링하고 입력 변화가 있을 때만 출력하는 코드가 포함되어 있지만 현재 주석 처리되어 있습니다. 사용할 경우 대상 비트 마스크와 하드웨어 입력 번호를 확인한 뒤 해당 블록의 주석을 해제하세요.

## 안전 및 주의 사항

- 실제 액추에이터가 연결된 상태에서 출력이 켜질 수 있으므로 최초 실행 전 배선과 동작 범위를 확인하세요.
- ESC 정상 종료 시에는 대상 출력을 OFF하지만, 네트워크 단절 시에는 종료 시퀀스가 실행되지 않을 수 있습니다. 하드웨어 레벨의 비상 정지와 인터록을 별도로 구성하세요.
- `Ez_Connect()`의 IP 문자열 형식이 잘못되면 `IPAddress.Parse()`에서 예외가 발생할 수 있습니다.
- 네이티브 API 반환 코드가 일부 래퍼 메서드에서 검사되지 않으므로, 운영 코드에서는 통신 실패와 장치 오류 처리를 보강하세요.
- 목표 비트마스크 컨트롤 장치를 namespace로 관리하는 것을 추천합니다.
  
## 빌드 확인 범위

소스 컴파일은 장치 없이 확인할 수 있습니다. 실제 연결과 I/O 동작은 FASTECH 장치, 올바른 네트워크 설정, 네이티브 DLL이 있는 Windows 환경에서 별도로 검증해야 합니다.
