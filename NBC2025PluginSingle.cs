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
