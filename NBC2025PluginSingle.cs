// NBC2025Plugin - ETABS 19 - NBC 105:2025
// All API calls verified from ETABSv1.dll
using System;
using System.Windows.Forms;
using ETABSv1;

namespace NBC2025Plugin
{
    public class NBC2025Plugin
    {
        private cSapModel _m;

        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                cHelper helper = new Helper();
                cOAPI oapi = helper.GetObject("CSI.ETABS.API.ETABSObject");
                cSapModel sapModel = oapi.SapModel;
                var plugin = new NBC2025Plugin();
                plugin.Run(sapModel);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Could not connect to ETABS 19.\nMake sure ETABS 19 is open.\n\nError: " + ex.Message,
                    "NBC 2025 - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

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
                double g     = 9.81;

                _m.SetPresentUnits(eUnits.kN_m_C);

                MakePat("EQX",     eLoadPatternType.Quake);
                MakePat("EQY",     eLoadPatternType.Quake);
                MakePat("EQX_SLS", eLoadPatternType.Quake);
                MakePat("EQY_SLS", eLoadPatternType.Quake);

                var    spec  = BuildSpectrum(Z, I, Rmu, OmegaU, Tc, Td, alpha, k);
                double[] Tv  = spec[0];
                double[] Sv  = spec[1];
                MakeRSFunc("NBC2025_ULS", Tv, Sv, 0.05);

                double   slsR  = (CdULS > 1e-10) ? CdSLS / CdULS : 0.2;
                double[] TvSLS = (double[])Tv.Clone();
                double[] SvSLS = new double[Sv.Length];
                for (int ii = 0; ii < Sv.Length; ii++) SvSLS[ii] = Sv[ii] * slsR;
                MakeRSFunc("NBC2025_SLS", TvSLS, SvSLS, 0.05);

                MakeRSCase("RSX", "NBC2025_ULS", "U1", g * CdULS, 0.05);
                MakeRSCase("RSY", "NBC2025_ULS", "U2", g * CdULS, 0.05);

                MakeAllCombos(D, SDL, L, RL, WX, WY);

                MessageBox.Show(
                    "NBC 105:2025 Plugin Complete!\n\n" +
                    "TIME PERIOD\n" +
                    "  T1       = " + T1.ToString("F4")    + " s\n" +
                    "  T_design = " + Tdes.ToString("F4")  + " s\n\n" +
                    "SITE SPECTRA\n" +
                    "  Soil=" + soil + "  Tc=" + Tc + "  Td=" + Td + "\n" +
                    "  Ch(T) = " + Ch.ToString("F4")    + "\n" +
                    "  C(T)  = " + CT.ToString("F4")    + "\n" +
                    "  Cs(T) = " + CsT.ToString("F4")   + "\n\n" +
                    "DESIGN COEFFICIENTS\n" +
                    "  Cd_ULS = " + CdULS.ToString("F5") + "\n" +
                    "  Cd_SLS = " + CdSLS.ToString("F5") + "\n" +
                    "  kd     = " + kd.ToString("F2")    + "\n\n" +
                    "BASE SHEAR\n" +
                    "  V_ULS = " + VULS.ToString("F1")   + " kN\n" +
                    "  V_SLS = " + VSLS.ToString("F1")   + " kN\n\n" +
                    "CREATED IN ETABS\n" +
                    "  Patterns : EQX EQY EQX_SLS EQY_SLS\n" +
                    "  RS Funcs : NBC2025_ULS NBC2025_SLS\n" +
                    "  RS Cases : RSX RSY\n" +
                    "  Combos   : 40 combinations\n\n" +
                    "Please re-run analysis.",
                    "NBC 105:2025 - Done",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:\n" + ex.Message,
                    "NBC 2025 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        double[][] BuildSpectrum(double Z, double I, double Rmu, double OmegaU,
                                  double Tc, double Td, double alpha, double k)
        {
            double[] Tv = {0.0,0.05,0.10,0.15,0.20,0.25,0.30,0.40,0.50,
                           0.60,0.70,0.80,0.90,1.00,1.20,1.40,1.60,1.80,
                           2.00,2.50,3.00,3.50,4.00,4.50,Td,Td+0.1,6.0,8.0,10.0};
            double[] Sv = new double[Tv.Length];
            for (int i=0;i<Tv.Length;i++)
                Sv[i] = CalcCh(Tv[i],Tc,Td,alpha,k)*Z*I/(Rmu*OmegaU);
            return new double[][]{Tv,Sv};
        }

        void MakePat(string name, eLoadP
