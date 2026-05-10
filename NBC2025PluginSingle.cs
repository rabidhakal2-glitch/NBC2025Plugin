using System;
using System.Windows.Forms;
using ETABSv1;

namespace NBC2025Plugin
{
    public class NBC2025Plugin
    {
        private cSapModel _m;

        public void Run(cSapModel SapModel)
        {
            _m = SapModel;
            try
            {
                var frm = new InputForm();
                if (frm.ShowDialog() != DialogResult.OK) return;

                double H      = frm.H;
                double Z      = frm.Z;
                double I      = frm.I;
                double Rmu    = frm.Rmu;
                double OmegaU = frm.OmegaU;
                double OmegaS = frm.OmegaS;
                double W      = frm.W;
                string soil   = frm.Soil;
                int    nStory = frm.NStory;
                double kt     = frm.kt;
                string D      = frm.Dead;
                string SDL    = frm.SDL;
                string L      = frm.Live;
                string RL     = frm.RoofLive;
                string WX     = frm.WindX;
                string WY     = frm.WindY;

                double T1   = kt * Math.Pow(H, 0.75);
                double Tdes = T1 * 1.25;

                double Ta, Tc, Td, alpha, k;
                GetSpectral(soil, out Ta, out Tc, out Td, out alpha, out k);

                double Ch    = CalcCh(Tdes, Tc, Td, alpha, k);
                double CT    = Ch * Z * I;
                double CsT   = 0.20 * CT;
                double CdULS = CT  / (Rmu * OmegaU);
                double CdSLS = CsT / OmegaS;
                double kd    = GetKd(nStory);
                double VULS  = CdULS * W;
                double VSLS  = CdSLS * W;

                _m.SetPresentUnits(eUnits.kN_m_C);

                MakePat("EQX",     eLoadPatternType.Quake);
                MakePat("EQY",     eLoadPatternType.Quake);
                MakePat("EQX_SLS", eLoadPatternType.Quake);
                MakePat("EQY_SLS", eLoadPatternType.Quake);

                MakeAllCombos(D, SDL, L, RL, WX, WY);

                string nl  = Environment.NewLine;
                string msg = "NBC 105:2025 Plugin Complete!" + nl + nl;
                msg += "T1 = " + T1.ToString("F4") + " s" + nl;
                msg += "T_design = " + Tdes.ToString("F4") + " s" + nl;
                msg += "Soil=" + soil + " Tc=" + Tc.ToString("F1") + " Td=" + Td.ToString("F1") + nl;
                msg += "Ch(T) = " + Ch.ToString("F4") + nl;
                msg += "C(T) = " + CT.ToString("F4") + nl;
                msg += "Cd_ULS = " + CdULS.ToString("F5") + nl;
                msg += "Cd_SLS = " + CdSLS.ToString("F5") + nl;
                msg += "kd = " + kd.ToString("F2") + nl;
                msg += "V_ULS = " + VULS.ToString("F1") + " kN" + nl;
                msg += "V_SLS = " + VSLS.ToString("F1") + " kN" + nl + nl;
                msg += "Patterns and Combos created. Re-run analysis.";
                MessageBox.Show(msg, "NBC 105:2025", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void GetSpectral(string soil, out double Ta, out double Tc,
                         out double Td, out double alpha, out double k)
        {
            switch (soil.ToUpper())
            {
                case "A": Ta=0.1;Tc=0.4;Td=4.0;alpha=2.50;k=1.8;break;
                case "B": Ta=0.1;Tc=0.5;Td=4.0;alpha=2.50;k=1.8;break;
                case "C": Ta=0.1;Tc=0.7;Td=4.0;alpha=2.50;k=1.8;break;
                default:  Ta=0.1;Tc=0.9;Td=5.0;alpha=2.25;k=1.8;break;
            }
        }

        double CalcCh(double T, double Tc, double Td, double alpha, double k)
        {
            if (T <= Tc)      return alpha;
            else if (T <= Td) return alpha * Math.Pow(Tc/T, k);
            else              return alpha * Math.Pow(Tc/Td, k) * Math.Pow(Td/T, 2.0);
        }

        double GetKd(int n)
        {
            if (n<=1) return 1.00;
            if (n==2) return 0.97;
            if (n==3) return 0.94;
            if (n==4) return 0.91;
            if (n==5) return 0.88;
            return 0.85;
        }

        void MakePat(string name, eLoadPatternType type)
        {
            try { _m.LoadPatterns.Add(name, type, 0, true); } catch {}
        }

        void MakeAllCombos(string D, string SDL, string L, string RL,
                            string WX, string WY)
        {
            string EX  = "EQX";
            string EY  = "EQY";
            string EXs = "EQX_SLS";
            string EYs = "EQY_SLS";

            LC("G1_DL1.5_SDL1.5_LL1.5", new string[]{D,SDL,L},  new double[]{1.5,1.5,1.5});
            LC("G2_DL1.5_SDL1.5_RL1.5", new string[]{D,SDL,RL}, new double[]{1.5,1.5,1.5});
            LC("G3_DL1.2_SDL1.2_LL1.6", new string[]{D,SDL,L},  new double[]{1.2,1.2,1.6});
            LC("G4_DL1.2_SDL1.2_RL1.6", new string[]{D,SDL,RL}, new double[]{1.2,1.2,1.6});
            LC("G5_DL0.9_SDL0.9",        new string[]{D,SDL},    new double[]{0.9,0.9});

            LC("S01_ULS_pX_p03Y", new string[]{D,SDL,L,EX,EY}, new double[]{1.2,1.2,0.3,+1.0,+0.3});
            LC("S02_ULS_pX_m03Y", new string[]{D,SDL,L,EX,EY}, new double[]{1.2,1.2,0.3,+1.0,-0.3});
            LC("S03_ULS_mX_p03Y", new string[]{D,SDL,L,EX,EY}, new double[]{1.2,1.2,0.3,-1.0,+0.3});
            LC("S04_ULS_mX_m03Y", new string[]{D,SDL,L,EX,EY}, new double[]{1.2,1.2,0.3,-1.0,-0.3});
            LC("S05_ULS_pY_p03X", new string[]{D,SDL,L,EY,EX}, new double[]{1.2,1.2,0.3,+1.0,+0.3});
            LC("S06_ULS_pY_m03X", new string[]{D,SDL,L,EY,EX}, new double[]{1.2,1.2,0.3,+1.0,-0.3});
            LC("S07_ULS_mY_p03X", new string[]{D,SDL,L,EY,EX}, new double[]{1
