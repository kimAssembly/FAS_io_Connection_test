using FASTECH;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace KIRO
{

    [Flags] // 비트 필드로 사용될 열거형임을 명시합니다.
    public enum IO_PORT : uint // 기본 타입을 uint로 명시하여 비트 연산을 더 명확하게 합니다.
    {
        NONE = 0x00000000,
        OUTPUT01 = 0x00000001,
        OUTPUT02 = 0x00000002,
        OUTPUT03 = 0x00000004,
        OUTPUT04 = 0x00000008,
        OUTPUT05 = 0x00000010,
        OUTPUT06 = 0x00000020,
        OUTPUT07 = 0x00000040,
        OUTPUT08 = 0x00000080,
        OUTPUT09 = 0x00000100,
        OUTPUT10 = 0x00000200,
        OUTPUT11 = 0x00000400,
        OUTPUT12 = 0x00000800,
        OUTPUT13 = 0x00001000,
        OUTPUT14 = 0x00002000,
        OUTPUT15 = 0x00004000,
        OUTPUT16 = 0x00008000,
        ALL_PORT = 0xFFFFFFFF
    }

    public class EzServo_Driver : IDisposable
    {
        #region Static Members for DevNum Management

        // 사용 가능한 디바이스 번호를 관리하는 정적 스택 (풀)
        public static readonly Stack<int> availableDevNums = new Stack<int>();
        private const int MAX_DEVICES = 16; // 최대 16개 장치까지 지원

        /// <summary>
        /// 클래스가 처음 로드될 때 한 번만 실행되는 정적 생성자입니다.
        /// 사용 가능한 디바이스 번호 풀을 초기화합니다.
        /// </summary>
        static EzServo_Driver()
        {
            // 0부터 (MAX_DEVICES - 1)까지의 번호를 스택에 추가합니다.
            // 역순으로 넣어야 0부터 꺼내 쓸 수 있습니다.
            for (int i = MAX_DEVICES - 1; i >= 0; i--)
            {
                availableDevNums.Push(i);
            }
        }

        #endregion

        #region Instance Members

        public bool IsConnect { get; private set; } = false;
        public bool IsAlarm { get; private set; } = false;
        public uint Output { get; set; } = 0x00000000; // 현재 출력 상태를 저장하는 변수

        // 인스턴스에 할당된 고유 디바이스 번호 (-1은 할당되지 않음을 의미)
        private int intDevNum = -1;
        private bool disposed = false; // IDisposable 패턴을 위한 플래그


        #endregion

        #region Connection Methods

        /// <summary>
        /// 드라이버에 연결합니다. 사용 가능한 DevNum을 자동으로 할당받습니다.
        /// </summary>
        /// <param name="devIp">연결할 드라이버의 IP 주소</param>
        /// <param name="isbLog">로그 사용 여부</param>
        /// <returns>연결 성공 여부</returns>
        public bool Ez_Connect(string devIp, bool isbLog = false)
        {
            if (IsConnect) return true; // 이미 연결된 경우

            // 사용 가능한 DevNum이 있는지 확인하고 할당받습니다.
            lock (availableDevNums) // 스택 접근은 스레드로부터 안전해야 합니다.
            {
                if (availableDevNums.Count == 0)
                {
                    // 더 이상 할당할 수 있는 DevNum이 없음
                    return false;
                }
                intDevNum = availableDevNums.Pop();
            }

            if (isbLog)
            {
                EziMOTIONPlusELib.FAS_SetLogPath(System.IO.Directory.GetCurrentDirectory());
                EziMOTIONPlusELib.FAS_SetLogLevel(EziMOTIONPlusELib.LOG_LEVEL.LOG_LEVEL_ALL);
            }
            // 드라이버 연결 시도
            if (EziMOTIONPlusELib.FAS_Connect(IPAddress.Parse(devIp), intDevNum) == false)
            {
                // 연결 실패 시 할당받은 번호를 다시 풀에 반납합니다.
                lock (availableDevNums)
                {
                    availableDevNums.Push(intDevNum);
                }
                intDevNum = -1; // 할당 해제
                return false;
            }

            IsConnect = true;
            return true;
        }

        /// <summary>
        /// 드라이버 연결을 해제하고 사용하던 DevNum을 반납합니다.
        /// </summary>
        public void Ez_Close()
        {
            if (!IsConnect) return;

            EziMOTIONPlusELib.FAS_Close(intDevNum);
            IsConnect = false;

            // 사용이 끝난 DevNum을 다시 풀에 반납합니다.
            lock (availableDevNums)
            {
                availableDevNums.Push(intDevNum);
            }
            intDevNum = -1; // 할당 해제
        }

        #endregion

        #region I/O Control

        /// <summary>
        /// 디지털 입력 상태를 읽어옵니다.
        /// </summary>
        /// <returns>입력 포트의 비트 값</returns>
        public uint GetDigitalInput()
        {
            if (!IsConnect) return 0;
            uint input = 0;
            uint latch = 0;
            EziMOTIONPlusELib.FAS_GetInput(intDevNum, ref input, ref latch);
            return input;
        }

        /// <summary>
        /// 디지털 출력을 설정합니다. (비효율적 - SetSingleOutput 사용 권장)
        /// </summary>
        /// <param name="data">출력할 포트의 비트 값 (CombinePorts 메서드 사용 권장)</param>
        public void SetDigitalOutput(uint data)
        {
            if (!IsConnect) return;
            // 라이브러리 사양에 따라 onMask와 offMask를 전달합니다.
            // 주의: 이 방식은 모든 비트를 한 번에 덮어씁니다.
            EziMOTIONPlusELib.FAS_SetOutput(intDevNum, data, ~data);
        }

        /// <summary>
        /// 지정된 단일 디지털 출력 포트의 상태를 설정합니다 (On/Off). (수정된 버전)
        /// </summary>
        /// <param name="portBit">제어할 포트의 비트 마스크 (예: IO_PORT.OUTPUT01)</param>
        /// <param name="isOn">포트를 켤지(true) 여부. false이면 끕니다.</param>
        public void SetSingleOutput(uint portBit, bool isOn)
        {
            // 1. 연결 상태 확인
            if (!IsConnect) return;

            // 2. FAS_SetOutput 함수 사용
            //    - isOn이 true이면: 해당 비트만 켜고 (onMask = portBit), 다른 비트는 끄지 않음 (offMask = 0)
            //    - isOn이 false이면: 다른 비트는 켜지 않고 (onMask = 0), 해당 비트만 끔 (offMask = portBit)
            uint onMask = isOn ? portBit : 0;
            uint offMask = isOn ? 0 : portBit;

            EziMOTIONPlusELib.FAS_SetOutput(intDevNum, onMask, offMask);
        }

        /// <summary>
        /// 현재 모든 디지털 출력 포트의 상태를 읽어옵니다. (이름 및 파라미터 수정됨)
        /// </summary>
        /// <returns>모든 출력 포트의 상태를 나타내는 uint 비트마스크. 실패 시 0.</returns>
        public uint GetDigitalOutputState() // 파라미터 제거, 이름 변경
        {
            if (!IsConnect) return 0; // 연결 안됐으면 0 반환

            uint currentOutput = 0;
            uint statusOutput = 0; // 이 변수는 FAS_GetOutput에서 사용되지 않을 수 있음

            // 실제 출력 상태를 읽어옵니다.
            int result = EziMOTIONPlusELib.FAS_GetOutput(intDevNum, ref currentOutput, ref statusOutput);


            return currentOutput; // 실제 읽은 값 반환
        }

        /// <summary>
        /// 여러 IO_PORT를 조합하여 하나의 uint 비트마스크로 만듭니다.
        /// </summary>
        /// <param name="ports">조합할 포트들</param>
        /// <returns>조합된 비트마스크 값</returns>
        public static uint CombinePorts(params IO_PORT[] ports)
        {
            uint combinedValue = 0;
            foreach (var port in ports)
            {
                combinedValue |= (uint)port;
            }
            return combinedValue;
        }

        #endregion

        #region 주어진 숫자 값의 비트를 지정된 IO 포트들에 매핑하여 최종 비트마스크를 생성합니다.
        /// <summary>
        /// 주어진 숫자 값의 비트를 지정된 IO 포트들에 매핑하여 최종 비트마스크를 생성합니다.
        /// </summary>
        /// <param name="value">포트에 매핑할 숫자 값 (예: 12)</param>
        /// <param name="startPort">매핑을 시작할 첫 번째 IO 포트 (예: IO_PORT.OUTPUT01)</param>
        /// <param name="numBits">사용할 비트의 수 (예: 4)</param>
        /// <returns>생성된 최종 IO 포트 비트마스크</returns>
        public uint MapValueToPorts(uint value, IO_PORT startPort, int numBits)
        {
            uint finalMask = 0;

            // 사용할 비트 수만큼 반복합니다 (0부터 3까지).
            for (int i = 0; i < numBits; i++)
            {
                // 1. 'value'의 i번째 비트가 1인지 확인합니다. (1U << i는 1, 2, 4, 8... 을 의미)
                if ((value & (1U << i)) != 0)
                {
                    // 2. 비트가 1이면, 해당하는 포트를 켭니다.
                    //    시작 포트에서 i만큼 왼쪽으로 비트 이동(shift)하면 다음 포트가 됩니다.
                    //    (예: OUTPUT01 << 2  =>  OUTPUT03)
                    finalMask |= ((uint)startPort << i);
                }
            }

            return finalMask;
        }
        #endregion

        #region 주어진 숫자 값의 비트를 지정된 IO 포트들에 매핑하여 최종 비트마스크를 생성합니다. (최대 16비트)
        /// <summary>
        /// 주어진 숫자 값의 비트를 지정된 IO 포트들에 매핑하여 최종 비트마스크를 생성합니다. (최대 16비트)
        /// </summary>
        /// <param name="value">포트에 매핑할 숫자 값 (int 타입, 예: 12)</param>
        /// <param name="startPort">매핑을 시작할 첫 번째 IO 포트 (예: IO_PORT.OUTPUT01)</param>
        /// <param name="numBits">사용할 비트의 수 (최대 16)</param>
        /// <returns>생성된 최종 IO 포트 비트마스크</returns>
        public uint MapValueToPorts(int value, IO_PORT startPort, int numBits)
        {
            uint finalMask = 0;

            // 사용할 비트 수만큼 반복하되, 최대 16비트를 넘지 않도록 합니다.
            for (int i = 0; i < numBits && i < 16; i++)
            {
                // 1. 'value'의 i번째 비트가 1인지 확인합니다. (int 타입에 맞춰 1 << i 사용)
                if ((value & (1 << i)) != 0)
                {
                    // 2. 비트가 1이면, 해당하는 포트의 비트를 켭니다.
                    //    (예: startPort가 OUTPUT01일 때 i=2이면, OUTPUT01 << 2 => OUTPUT03)
                    finalMask |= ((uint)startPort << i);
                }
            }

            return finalMask;
        }
        #endregion

        public void SetOutput(int Value)
        {
            var outputMask = MapValueToPorts(Value, IO_PORT.OUTPUT01, 16); // 최대 16비트까지 지원
            SetDigitalOutput(outputMask);
        }


        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); // GC가 소멸자를 호출하지 않도록 요청
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;

            if (disposing)
            {
                // 관리되는 리소스 해제 (여기에 해당하는 것이 있다면)
            }

            // 관리되지 않는 리소스 해제 (연결 해제 등)
            Ez_Close();

            disposed = true;
        }

        // 소멸자: Dispose가 호출되지 않은 경우를 위한 최후의 안전장치
        ~EzServo_Driver()
        {
            Dispose(false);
        }

        #endregion
    }
}
