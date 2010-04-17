using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace SharpBoy2.Emulation
{
    public class CPU
    {
        //Constants
        public const int CYCLESPERSECOND = 4194304;
        public const int CYCLESPERFRAME = CYCLESPERSECOND / 60;
        public const byte FLAG_Z = 7;
        public const byte FLAG_N = 6;
        public const byte FLAG_H = 5;
        public const byte FLAG_C = 4;

        private byte[] BIOS;

        public int DIV_Counter;
        public int TIMA_Counter;
        public int[] TIMA_Frequencies;
        public int CyclesRunThisFrame;
        public bool InterruptMasterEnable;
        public ushort[] InterruptAddresses;

        public Register AF;
        public Register BC;
        public Register DE;
        public Register HL;
        public Register StackPointer;
        public Register ProgramCounter;

        public Core MyCore;

        public CPU(Core C)
        {
            AF = new Register();
            BC = new Register();
            DE = new Register();
            HL = new Register();
            StackPointer = new Register();
            ProgramCounter = new Register();
            MyCore = C;

            using (System.IO.FileStream fs = new System.IO.FileStream(Application.ExecutablePath.Substring(0,Application.ExecutablePath.LastIndexOf("\\")+1) + "DMG_ROM.bin", System.IO.FileMode.Open))
            {
                BIOS = new byte[fs.Length];
                fs.Read(BIOS, 0, (int)fs.Length);
                fs.Close();
            }
            
            TIMA_Frequencies = new int[] { 4096, 262144, 65536, 16384 };
            InterruptAddresses = new ushort[] { 0x40, 0x48, 0x50, 0x58, 0x60 };
            InterruptMasterEnable = true;
        }

        public void Reset()
        {
            AF.High = 0x01;
            AF.Low = 0xB0;
            BC.Word = 0x0013;
            DE.Word = 0x00D8;
            HL.Word = 0x014D;
            StackPointer.Word = 0xFFFE;
            ProgramCounter.Word = 0;

            Array.Copy(BIOS, 0, MyCore.MyMemory.GameBoyRAM, 0, BIOS.Length);

            DIV_Counter = 256;
            TIMA_Counter = 1024;
            InterruptMasterEnable = true;
        }

        public void DoFrame()
        {
            CyclesRunThisFrame = 0;

            while (CyclesRunThisFrame < CYCLESPERFRAME)
            {
                DoStep();
            }

        }

        public void DoStep()
        {
            int Cycles = DoOpcode();

            UpdateSpecialRegisters(Cycles);
            DoInterrupts(Cycles);

            CyclesRunThisFrame += Cycles;
        }

        public int DoOpcode()
        {
            byte Opcode = MyCore.MyMemory.Read(ProgramCounter.Word);
            sbyte Jumpval;
            int CyclesUsed = 0;
            switch(Opcode)
            {
                case(0x00): //NOP
                    CyclesUsed = 1;
                    break;
                case(0x01): //LD BC,nn
                    LD_R16_NN(ref BC, MyCore.MyMemory.ReadWord(++ProgramCounter.Word));
                    CyclesUsed = 3;
                    break;
                case(0x02): //LD (BC),A
                    Utility.SetBit(ref AF.Low, FLAG_Z, SBMode.Off);
                    Utility.SetBit(ref AF.Low, FLAG_N, SBMode.Off);
                    MyCore.MyMemory.Write(BC.Word, AF.High);
                    CyclesUsed = 2;
                    break;
                case(0x03): //INC BC
                    if (BC.Word == 0xFFFF)
                    {
                        BC.Word = 0;
                    }
                    else
                    {
                        BC.Word++;
                    }
                    CyclesUsed = 2;
                    break;
                case(0x04): //INC B
                    INC_R8(ref BC.High);
                    CyclesUsed = 1;
                    break;
                case(0x05): //DEC B
                    DEC_R8(ref BC.Low);
                    CyclesUsed = 1;
                    break;
                case(0x06): //LD B,n
                    Utility.SetBit(ref AF.Low, FLAG_Z, SBMode.Off);
                    Utility.SetBit(ref AF.Low, FLAG_N, SBMode.Off);
                    BC.High = MyCore.MyMemory.Read(++ProgramCounter.Word);
                    CyclesUsed = 2;
                    break;
                case(0x07): //RLC A
                    RLC_R8(ref AF.High);
                    CyclesUsed = 2;
                    break;
                case(0x08): //LD (nn),SP
                    AF.Low = 0;
                    MyCore.MyMemory.Write(++ProgramCounter.Word, StackPointer.Low);
                    MyCore.MyMemory.Write(++ProgramCounter.Word, StackPointer.High);
                    CyclesUsed = 5;
                    break;
                case(0x09): //ADD HL,BC
                    ADD_R16_R16(ref HL, BC);
                    CyclesUsed = 2;
                    break;
                case(0x0A): //LD A,(BC)
                    AF.Low = 0;
                    AF.High = MyCore.MyMemory.Read(BC.Word);
                    CyclesUsed = 2;
                    break;
                case(0x0B): //DEC BC
                    if (BC.Word == 0xFFFF)
                    {
                        BC.Word = 0;
                    }
                    else
                    {
                        BC.Word++;
                    }
                    CyclesUsed = 2;
                    break;
                case(0x0C): //INC C
                    INC_R8(ref BC.Low);
                    CyclesUsed = 1;
                    break;
                case(0x0D): //DEC C
                    DEC_R8(ref BC.Low);
                    CyclesUsed = 1;
                    break;
                case(0x0E): //LD C,n
                    Utility.SetBit(ref AF.Low, FLAG_Z, SBMode.Off);
                    Utility.SetBit(ref AF.Low, FLAG_N, SBMode.Off);
                    BC.Low = MyCore.MyMemory.Read(++ProgramCounter.Word);
                    CyclesUsed = 2;
                    break;
                case(0x0F): //RRC A
                    RRC_R8(ref AF.High);
                    CyclesUsed = 2;
                    break;
                case(0x10): //STOP
                    CyclesUsed = 1;
                    break;
                case(0x11): //LD DE,nn
                    LD_R16_NN(ref DE, MyCore.MyMemory.ReadWord(++ProgramCounter.Word));
                    ++ProgramCounter.Word;
                    CyclesUsed = 3;
                    break;
                case(0x12): //LD (DE),A
                    Utility.SetBit(ref AF.Low, FLAG_Z, SBMode.Off);
                    Utility.SetBit(ref AF.Low, FLAG_N, SBMode.Off);
                    MyCore.MyMemory.Write(BC.Word, AF.High);
                    CyclesUsed = 2;
                    break;
                case(0x13): //INC DE
                    if (BC.Word == 0xFFFF)
                    {
                        BC.Word = 0;
                    }
                    else
                    {
                        BC.Word++;
                    }
                    CyclesUsed = 2;
                    break;
                case(0x14): //INC D
                    INC_R8(ref DE.High);
                    CyclesUsed = 1;
                    break;
                case(0x15): //DEC D
                    DEC_R8(ref DE.High);
                    CyclesUsed = 1;
                    break;
                case(0x16): //LD D,n
                    Utility.SetBit(ref AF.Low, FLAG_Z, SBMode.Off);
                    Utility.SetBit(ref AF.Low, FLAG_N, SBMode.Off);
                    DE.High = MyCore.MyMemory.Read(++ProgramCounter.Word);
                    CyclesUsed = 2;
                    break;
                case(0x17): //RL A
                    RL_R8(ref AF.High);
                    CyclesUsed = 2;
                    break;
                case(0x18): //JR n
                    Jumpval = (sbyte)MyCore.MyMemory.Read(++ProgramCounter.Word);
                    if (Jumpval < 0)
                    {
                        ProgramCounter.Word -= (ushort)Math.Abs(Jumpval);
                    }
                    else
                    {
                        ProgramCounter.Word += (ushort)Math.Abs(Jumpval);
                    }
                    CyclesUsed = 3;
                    break;
                case(0x19): //ADD HL,DE
                    ADD_R16_R16(ref HL, DE);
                    CyclesUsed = 2;
                    break;
                case(0x1A): //LD A,(DE)
                    AF.High = MyCore.MyMemory.Read(DE.Word);
                    CyclesUsed = 2;
                    break;
                case(0x1B): //DEC DE
                    if (DE.Word == 0xFFFF)
                    {
                        DE.Word = 0;
                    }
                    else
                    {
                        DE.Word++;
                    }
                    CyclesUsed = 2;
                    break;
                case(0x1C): //INC E
                    INC_R8(ref DE.Low);
                    CyclesUsed = 1;
                    break;
                case(0x1D): //DEC E
                    DEC_R8(ref DE.Low);
                    CyclesUsed = 1;
                    break;
                case(0x1E): //LD E,n
                    Utility.SetBit(ref AF.Low, FLAG_Z, SBMode.Off);
                    Utility.SetBit(ref AF.Low, FLAG_N, SBMode.Off);
                    DE.Low = MyCore.MyMemory.Read(++ProgramCounter.Word);
                    CyclesUsed = 2;
                    break;
                case(0x1F): //RR A
                    RR_R8(ref AF.High);
                    CyclesUsed = 2;
                    break;
                case(0x20): //JR NZ,n
                    Jumpval = (sbyte)MyCore.MyMemory.Read(++ProgramCounter.Word);
                    if (!Utility.IsBitSet(AF.Low, FLAG_Z))
                    {
                        if (Jumpval < 0)
                        {
                            ProgramCounter.Word -= (ushort)Math.Abs(Jumpval);
                        }
                        else
                        {
                            ProgramCounter.Word += (ushort)Math.Abs(Jumpval);
                        }
                    }
                    CyclesUsed = 3;
                    break;
                case(0x21): //LD HL,nn
                    LD_R16_NN(ref HL, MyCore.MyMemory.ReadWord(++ProgramCounter.Word));
                    ProgramCounter.Word++;
                    CyclesUsed = 3;
                    break;
                case(0x22): //LDI (HL),A
                    MyCore.MyMemory.Write(HL.Word, AF.High);
                    INC_R16(ref HL);
                    CyclesUsed = 2;
                    break;
                case(0x23): //INC HL
                    INC_R16(ref HL);
                    CyclesUsed = 2;
                    break;
                case(0x24): //INC H
                    INC_R8(ref HL.High);
                    CyclesUsed = 1;
                    break;
                case(0x25): //DEC H
                    DEC_R8(ref HL.Low);
                    CyclesUsed = 1;
                    break;
                case(0x26): //LD H,n
                    HL.High = MyCore.MyMemory.Read(++ProgramCounter.Word);
                    CyclesUsed = 2;
                    break;
                case(0x27): //DAA

                    if (Utility.IsBitSet(AF.Low, FLAG_N))
                    {
                        if ((AF.High & 0x0F) > 0x09 || (AF.Low & 0x20) != 0)
                        {
                            AF.High -= 0x06;
                            if ((AF.High & 0xF0) == 0xF0)
                            {
                                Utility.SetBit(ref AF.Low, FLAG_C, SBMode.On);
                            }
                            else
                            {
                                Utility.SetBit(ref AF.Low, FLAG_C, SBMode.Off);
                            }
                        }
                        if ((AF.High & 0xF0) > 0x90 || (AF.High & 0x10) != 0)
                        {
                            AF.High -= 0x60;
                        }
                    }
                    else
                    {
                        if ((AF.High & 0x0F) > 9 || (AF.High & 0x20) != 0)
                        {
                            AF.High += 0x06;
                            if ((AF.High & 0xF0) == 0)
                            {
                                Utility.SetBit(ref AF.Low, FLAG_C, SBMode.On);
                            }
                            else
                            {
                                Utility.SetBit(ref AF.Low, FLAG_C, SBMode.Off);
                            }
                        }
                        if ((AF.High & 0xF0) > 0x90 || Utility.IsBitSet(AF.Low, FLAG_C))
                        {
                            AF.High += 0x60;
                        }
                    }
                    if (AF.High == 0)
                    {
                        Utility.SetBit(ref AF.Low, FLAG_Z, SBMode.On);
                    }
                    else
                    {
                        Utility.SetBit(ref AF.Low, FLAG_Z, SBMode.Off);
                    }

                    CyclesUsed = 1;
                    break;
                case(0x28): //JR Z,n
                    Jumpval = (sbyte)MyCore.MyMemory.Read(++ProgramCounter.Word);
                    if (Utility.IsBitSet(AF.Low, FLAG_Z))
                    {
                        if (Jumpval < 0)
                        {
                            ProgramCounter.Word -= (ushort)Math.Abs(Jumpval);
                        }
                        else
                        {
                            ProgramCounter.Word += (ushort)Math.Abs(Jumpval);
                        }
                    }
                    CyclesUsed = 3;
                    break;
                case(0x29): //ADD HL,HL
                    ADD_R16_R16(ref HL, HL);
                    CyclesUsed = 2;
                    break;
                case(0x2A): //LDI A,(HL)
                    AF.High = MyCore.MyMemory.Read(HL.Word);
                    INC_R16(ref HL);
                    CyclesUsed = 2;
                    break;
                case(0x2B): //DEC HL
                    DEC_R16(ref HL);
                    CyclesUsed = 2;
                    break;
                case(0x2C): //INC L
                    INC_R8(ref HL.Low);
                    CyclesUsed = 1;
                    break;
                case(0x2D): //DEC L
                    DEC_R8(ref HL.Low);
                    CyclesUsed = 1;
                    break;
                case(0x2E): //LD L,n
                    HL.Low = MyCore.MyMemory.Read(++ProgramCounter.Word);
                    CyclesUsed = 2;
                    break;
                case(0x2F): //CPL
                    AF.High = (byte)(~AF.High);
                    Utility.SetBit(ref AF.Low, FLAG_N, SBMode.On);
                    Utility.SetBit(ref AF.Low, FLAG_H, SBMode.On);
                    CyclesUsed = 1;
                    break;
            }
            ProgramCounter.Word++;
            return CyclesUsed;
        }

        public void ADD_R16_R16(ref Register R16A, Register R16B)
        {
            Utility.SetBit(ref AF.Low, FLAG_N, SBMode.Off);
            if ((R16A.High & 0x0F) + (R16B.High & 0x0F) != 0)
            {
                Utility.SetBit(ref AF.Low, FLAG_H, SBMode.On);
            }
            else
            {
                Utility.SetBit(ref AF.Low, FLAG_H, SBMode.Off);
            }
            if ((int)(R16A.Word + R16B.Word) > 0xFFFF)
            {
                Utility.SetBit(ref AF.Low, FLAG_C, SBMode.On);
                R16A.Word = (ushort)((R16A.Word + R16B.Word) - 0xFFFF);
            }
            else
            {
                R16A.Word += R16B.Word;
            }
        }

        public void LD_R16_NN(ref Register R16, ushort NN)
        {
            Utility.SetBit(ref AF.Low, FLAG_Z, SBMode.Off);
            Utility.SetBit(ref AF.Low, FLAG_N, SBMode.Off);
            R16.Word = NN;
        }

        public void INC_R16(ref Register R16)
        {
            if (R16.Word == 0xFFFF)
            {
                R16.Word = 0;
            }
            else
            {
                R16.Word++;
            }
        }

        public void DEC_R16(ref Register R16)
        {
            if (R16.Word == 0)
            {
                R16.Word = 0xFFFF;
            }
            else
            {
                R16.Word--;
            }
        }

        public void INC_R8(ref byte R8)
        {
            if ((R8 & 0xF) == 0xF)
            {
                Utility.SetBit(ref AF.Low, FLAG_H, SBMode.On);
            }
            else
            {
                Utility.SetBit(ref AF.Low, FLAG_H, SBMode.Off);
            }
            Utility.SetBit(ref AF.Low, FLAG_N, SBMode.Off);
            if (R8 == 0xFF)
            {
                R8 = 0;
                Utility.SetBit(ref AF.Low, FLAG_Z, SBMode.On);
            }
            else
            {
                R8++;
                Utility.SetBit(ref AF.Low, FLAG_Z, SBMode.Off);
            }
        }

        public void DEC_R8(ref byte R8)
        {
            if (R8 == 0)
            {
                Utility.SetBit(ref AF.Low, FLAG_Z, SBMode.Off);
            }
            if ((R8 & 0xF) == 0)
            {
                Utility.SetBit(ref AF.Low, FLAG_H, SBMode.On);
            }
            else
            {
                Utility.SetBit(ref AF.Low, FLAG_H, SBMode.Off);
            }
            Utility.SetBit(ref AF.Low, FLAG_N, SBMode.On);
            if (R8 == 0)
            {
                R8 = 0xFF;
            }
            else
            {
                R8--;
                if (R8 == 0)
                {
                    Utility.SetBit(ref AF.Low, FLAG_Z, SBMode.On);
                }
            }
        }

        public void RLC_R8(ref byte R8)
        {
            bool CarrySet = Utility.IsBitSet(AF.Low, FLAG_C);
            bool MSBSet = Utility.IsBitSet(R8, 7);

            AF.Low = 0;
            R8 <<= 1;

            if (MSBSet)
            {
                Utility.SetBit(ref AF.Low, FLAG_C, SBMode.On);
            }
            if (CarrySet)
            {
                Utility.SetBit(ref R8, 0, SBMode.On);
            }
            if (R8 == 0)
            {
                Utility.SetBit(ref AF.Low, FLAG_Z, SBMode.On);
            }             
        }

        public void RRC_R8(ref byte R8)
        {
            bool CarrySet = Utility.IsBitSet(AF.Low, FLAG_C);
            bool LSBSet = Utility.IsBitSet(R8, 0);

            AF.Low = 0;
            R8 >>= 1;

            if (LSBSet)
            {
                Utility.SetBit(ref AF.Low, FLAG_C, SBMode.On);
            }
            if (CarrySet)
            {
                Utility.SetBit(ref R8, 7, SBMode.On);
            }
            if (R8 == 0)
            {
                Utility.SetBit(ref AF.Low, FLAG_Z, SBMode.On);
            } 
        }

        public void RL_R8(ref byte R8)
        {
            AF.Low = 0;
            bool MSBSet = Utility.IsBitSet(R8, 7);
            R8 <<= 1;
            if (MSBSet)
            {
                Utility.SetBit(ref AF.Low, FLAG_C, SBMode.On);
                Utility.SetBit(ref R8, 0, SBMode.On);
            }

            if (R8 == 0)
            {
                Utility.SetBit(ref AF.Low, FLAG_Z, SBMode.On);
            }
        }

        public void RR_R8(ref byte R8)
        {
            AF.Low = 0;
            bool LSBSet = Utility.IsBitSet(R8, 0);
            R8 >>= 1;
            if (LSBSet)
            {
                Utility.SetBit(ref AF.Low, FLAG_C, SBMode.On);
                Utility.SetBit(ref R8, 7, SBMode.On);
            }

            if (R8 == 0)
            {
                Utility.SetBit(ref AF.Low, FLAG_Z, SBMode.On);
            }
        }

        private void CALL(ushort Address)
        {
            PUSH(ProgramCounter.Word);
            ProgramCounter.Word = Address;
        }

        private void RET()
        {
            ProgramCounter.Word = POP();
        }

        private void RETI()
        {
            InterruptMasterEnable = true;
            RET();
        }

        private void PUSH(ushort NN)
        {
            MyCore.MyMemory.Write((ushort)(StackPointer.Word - 1), (byte)((NN & 0xFF00) >> 8));
            MyCore.MyMemory.Write((ushort)(StackPointer.Word - 2), (byte)(NN & 0x00FF));
            StackPointer.Word -= 2;
        }

        private ushort POP()
        {
            ushort ret = (ushort)(MyCore.MyMemory.Read((ushort)(StackPointer.Word + 1)) << 8);
            ret |= MyCore.MyMemory.Read(StackPointer.Word);
            StackPointer.Word += 2;
            return ret;
        }

        public void DoInterrupts(int Cycles)
        {
	        if(InterruptMasterEnable)
	        {
		        for(byte i=0;i<8;i++)
		        {
			        if(Utility.IsBitSet(MyCore.MyMemory.Read(0xFFFF),i))
			        {
				        Utility.SetBit(ref MyCore.MyMemory.GameBoyRAM[0xFF0F],i,SBMode.Off);
				        InterruptMasterEnable = false;
				        CALL(InterruptAddresses[i]);
				        break;
			        }
		        }
	        }   
        }



        public void UpdateSpecialRegisters(int Cycles)
        {
            //DIV
            DIV_Counter -= Cycles;
            if (DIV_Counter <= 0)
            {
		        if(MyCore.MyMemory.GameBoyRAM[0xFF04] == 0xFF)
		        {
		        	MyCore.MyMemory.GameBoyRAM[0xFF04] = 0;
		        }
		        else
    	    	{
    	    		MyCore.MyMemory.GameBoyRAM[0xFF04]++;
	        	}
		        DIV_Counter = 256;
            }

	        //TIMA
	        TIMA_Counter -= Cycles;
	        if(TIMA_Counter <= 0)
	        {
		        if(MyCore.MyMemory.GameBoyRAM[0xFF05] == 0xFF)
		        {
		        	MyCore.MyMemory.GameBoyRAM[0xFF05] = MyCore.MyMemory.GameBoyRAM[0xFF06];
			        Utility.SetBit(ref MyCore.MyMemory.GameBoyRAM[0xFF0F],2,SBMode.On);
		        }
		        else
		        {
		        	MyCore.MyMemory.GameBoyRAM[0xFF05]++;
		        }
		        TIMA_Counter = CYCLESPERSECOND / TIMA_Frequencies[MyCore.MyMemory.GameBoyRAM[0xFF07] & 3];
            }
        }
    }

    public struct Register
    {
        public byte High;
        public byte Low;

        public ushort Word
        {
            get { return (ushort)((High << 8) + Low); }
            set { Low = (byte)(value & 0x00FF); High = (byte)((value & 0xFF00) >> 8); }
        }
    }
}