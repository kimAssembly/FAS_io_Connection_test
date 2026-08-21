using System;
using System.Threading;
using System.Threading.Tasks;
using KIRO; // EzServo_Driver가 포함된 네임스페이스

namespace FAS_io_Connection_test
{
    class Program
    {
        // 💡 중요: 물리 포트 O7/I7은 8번째 비트이므로 0x80 입니다.
        // EzServo_Driver의 IO_PORT 열거형 기준으로는 OUTPUT08 입니다.
        const uint TARGET_OUTPUT_PORT1 = (uint)IO_PORT.OUTPUT15;
        const uint TARGET_OUTPUT_PORT2 = (uint)IO_PORT.OUTPUT16;
        const uint TARGET_INPUT_PORT = (uint)IO_PORT.OUTPUT08;

        static async Task Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("           I/O Interface Test  ");
            Console.WriteLine("========================================\n");

            Console.WriteLine("디바이스 접속 중... (IP: 192.168.0.2)");

            using (EzServo_Driver ioDriver = new EzServo_Driver())
            {
                // 1. 연결 시도
                if (!ioDriver.Ez_Connect("192.168.0.2"))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("연결 실패! IP 주소나 랜선 연결 상태, 파스텍 전원을 확인해주세요.");
                    Console.ResetColor();
                    Console.ReadLine();
                    return;
                }

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("연결 성공!");
                Console.ResetColor();
                Console.WriteLine("----------------------------------------");
                Console.WriteLine("[Space] 키 : Output 6, 7 토글 (ON/OFF 명령)");
                Console.WriteLine("[N] 키 : Output 6 토글 / [M] 키 : Output 7 토글");
                Console.WriteLine("[ESC] 키   : 프로그램 안전 종료");
                Console.WriteLine("----------------------------------------\n");

                bool isRunning = true;
                bool currentOutputState = false;

                // 2. 입력(Input) 상태 모니터링을 위한 백그라운드 스레드 (Polling)
                 /*Task.Run(() =>
                {
                    bool lastInputState = false;

                    while (isRunning)
                    {
                        uint currentInput = ioDriver.GetDigitalInput();
                        bool isInput7On = (currentInput & TARGET_INPUT_PORT) != 0;

                        // 상태가 변했을 때만 콘솔에 출력
                         if (isInput7On != lastInputState)
                        {
                            if (isInput7On)
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"[입력 감지] Input 7 신호가 들어왔습니다! (ON)");
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.DarkGray;
                                Console.WriteLine($"[입력 해제] Input 7 신호가 끊어졌습니다. (OFF)");
                            }
                            Console.ResetColor();
                            lastInputState = isInput7On;
                        }
                        Thread.Sleep(50);
                    }
                });*/

                // 3. 출력 관리
                /*while (isRunning)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true).Key;

                        if (key == ConsoleKey.Spacebar)
                        {
                            // 상태 반전 및 출력 전송

                            currentOutputState = !currentOutputState;

                            ioDriver.SetSingleOutput(TARGET_OUTPUT_PORT1, currentOutputState);
                            await Task.Delay(5000);
                            ioDriver.SetSingleOutput(TARGET_OUTPUT_PORT2, currentOutputState);

                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"[출력 명령] Output 7 상태를 {(currentOutputState ? "ON" : "OFF")} 으로 변경했습니다.");
                            Console.ResetColor();
                        }
                        else if (key == ConsoleKey.Escape)
                        {
                            Console.WriteLine("\n종료 시퀀스 시작: 모든 출력을 차단합니다...");
                            isRunning = false;

                            // 안전(Fail-Safe)을 위해 프로그램 종료 전 출력 강제 OFF
                            ioDriver.SetSingleOutput(TARGET_OUTPUT_PORT1, false);
                            ioDriver.SetSingleOutput(TARGET_OUTPUT_PORT2, false);
                            break;
                        }
                    }
                    Thread.Sleep(10); // CPU 점유율 안정화
                }*/

                while (isRunning)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true).Key;
                        int delaytime = 5000;
                        if (key == ConsoleKey.Spacebar)
                        {
                            if (!currentOutputState)
                            {
                                currentOutputState = true;

                                ioDriver.SetSingleOutput(TARGET_OUTPUT_PORT1, true);
                                await Task.Delay(delaytime);
                                ioDriver.SetSingleOutput(TARGET_OUTPUT_PORT2, true);

                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("[출력] PORT1 -> PORT2 순차적으로 ON 시켰습니다.");
                                Console.ResetColor();
                            }
                            else
                            {
                                currentOutputState = false;

                                ioDriver.SetSingleOutput(TARGET_OUTPUT_PORT2, false);
                                await Task.Delay(delaytime);
                                ioDriver.SetSingleOutput(TARGET_OUTPUT_PORT1, false);

                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("[출력] PORT2 -> PORT1 순차적으로 OFF 시켰습니다.");
                                Console.ResetColor();
                            }
                        }
                        else if (key == ConsoleKey.N)
                        {
                            currentOutputState = !currentOutputState;
                            ioDriver.SetSingleOutput(TARGET_OUTPUT_PORT1, currentOutputState);
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"[출력] PORT1 상태를 {(currentOutputState ? "ON" : "OFF")} 으로 변경했습니다.");
                            Console.ResetColor();
                        }
                        else if (key == ConsoleKey.M)
                        {
                            currentOutputState = !currentOutputState;
                            ioDriver.SetSingleOutput(TARGET_OUTPUT_PORT2, currentOutputState);
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"[출력] PORT2 상태를 {(currentOutputState ? "ON" : "OFF")} 으로 변경했습니다.");
                            Console.ResetColor();
                        }
                        else if (key == ConsoleKey.Escape)
                        {
                            Console.WriteLine("\n종료 시퀀스 시작: 모든 출력을 차단합니다.");
                            isRunning = false;

                            ioDriver.SetSingleOutput(TARGET_OUTPUT_PORT1, false);
                            ioDriver.SetSingleOutput(TARGET_OUTPUT_PORT2, false);
                            break;
                        }
                    }
                    Thread.Sleep(10); // CPU 점유율 안정화
                }
            }

            Console.WriteLine("프로그램이 안전하게 종료되었습니다.");
        }
    }
}
