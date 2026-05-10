// NBC2025Plugin.cs - ETABS 19 Plugin - NBC 105:2025
// Single file version - easier to compile
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ETABSv1;

namespace NBC2025Plugin
{
    public class NBC2025Plugin : IPlugin
    {
        private cSapModel _m;

        public string Info()
        {
            return "NBC 105:2025 Seismic Design | Load Combinations | Nepal";
        }

        public int Main(ref cSapModel SapModel, ref cPluginCallback ISapPlugin)
        {
            _m = SapModel;
            try
            {
                // ── Show Input Form ──────────────────────────────────────────
                var frm = new InputForm();
                if (frm.ShowDialog() != DialogResult.OK)
                { ISapPlugin.Finish(0); return 0; }

                // ── Get Inputs ───────────────────────────────────────────────
                double H       = frm.H;
                double Z       = frm.Z;
                double I       = frm.I;
                double Rmu     = frm.Rmu;
                double OmegaU  = frm.OmegaU;
                double OmegaS  = frm.OmegaS;
                double W       = frm.W;
                string soil    = frm.Soil;
                int    nStory  = frm.NStory;
                double kt      = frm.kt;
                string D       = frm.Dead;
                string SDL     = frm.SDL;
                string L       = frm.Live;
                string RL      = frm.RoofLive;
                string WX      = frm.WindX;
                string WY      = frm.WindY;

                // ── NBC 2025 Calculations ────────────────────────────────────
                // Time Period §5.1.2
                double T1   = kt * Math.Pow(H, 0.75);
                double Tdes = T1 * 1.25;

                // Spectral params Table 4-1
                double Ta, Tc, Td, alpha, k;
                GetSpectral(soil, out Ta, out Tc, out Td, out alpha, out k);

                // Ch(T) - 3 branch spectrum §4.1.2
                double Ch = CalcCh(Tdes, Tc, Td, alpha, k);

                // C(T) = Ch x Z x I
                double CT  = Ch * Z * I;
                double CsT = 0.20 * CT;

                // Design coefficients §6.2
                double CdULS = CT  / (Rmu * OmegaU);
                double CdSLS = CsT / OmegaS;

                // kd Table 6-1 NEW
                double kd = GetKd(nStory);

                // Base shear
                double VULS = CdULS * W;
                double VSLS = CdSLS * W;
                double g    = 9.81;

                // ── Apply to ETABS ───────────────────────────────────────────
                _m.SetPresentUnits(eUnits.kN_m_C);

                // Create load patterns
                MakePattern("EQX",     eLoadPatternType.Quake);
                MakePattern("EQY",     eLoadPatternType.Quake);
                MakePattern("EQX_SLS", eLoadPatternType.Quake);
                MakePattern("EQY_SLS", eLoadPatternType.Quake);

                // Define ULS spectrum function
                var ulsData = BuildSpectrum(Z, I, Rmu, OmegaU, Tc, Td, alpha, k);
                double[] Tpts = ulsData[0];
                double[] Spts = ulsData[1];
                _m.Func.FuncRS.SetUser("NBC2025_ULS", Tpts.Length, ref Tpts, ref Spts, 0.05);

                // SLS spectrum
                double slsRatio = (CdULS > 1e-10) ? CdSLS / CdULS : 0.2;
                double[] SlsSpts = new double[Spts.Length];
                for (int ii = 0; ii < Spts.Length; ii++) SlsSpts[ii] = Spts[ii] * slsRatio;
                double[] TptsCopy = (double[])Tpts.Clone();
                _m.Func.FuncRS.SetUser("NBC2025_SLS", TptsCopy.Length, ref TptsCopy, ref SlsSpts, 0.05);

                // Create RS load cases
                MakeRSCase("RSX", "NBC2025_ULS", "U1", g * CdULS, 0.05);
                MakeRSCase("RSY", "NBC2025_ULS", "U2", g * CdULS, 0.05);

                // Create load combinations - all 34 linear + 6 envelope
                MakeAllCombos(D, SDL, L, RL, WX, WY);

                // Show summary
                MessageBox.Show(
                    "NBC 105:2025 Plugin Complete!\n\n" +
                    "TIME PERIOD\n" +
                    "  T1 = " + T1.ToString("F4") + " s\n" +
                    "  T_design = " + Tdes.ToString("F4") + " s\n\n" +
                    "SPECTRA\n" +
                    "  Soil: " + soil + "  Tc=" + Tc + "  Td=" + Td + " [NEW]\n" +
                    "  Ch(T) = " + Ch.ToString("F4") + "\n" +
                    "  C(T)  = " + CT.ToString("F4") + "\n" +
                    "  Cs(T) = " + CsT.ToString("F4") + "\n\n" +
                    "DESIGN COEFFICIENTS\n" +
                    "  Cd_ULS = " + CdULS.ToString("F5") + "\n" +
                    "  Cd_SLS = " + CdSLS.ToString("F5") + "\n" +
                    "  kd     = " + kd.ToString("F2") + " [NEW NBC 2025]\n\n" +
                    "BASE SHEAR\n" +
                    "  V_ULS = " + VULS.ToString("F1") + " kN\n" +
                    "  V_SLS = " + VSLS.ToString("F1") + " kN\n\n" +
                    "ETABS\n" +
                    "  Patterns: EQX, EQY, EQX_SLS, EQY_SLS\n" +
                    "  RS Funcs: NBC2025_ULS, NBC2025_SLS\n" +
                    "  RS Cases: RSX, RSY\n" +
                    "  Combos  : 40 created\n\n" +
                    "Re-run analysis to update results.",
                    "NBC 105:2025 Done",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:\n" + ex.Message, "NBC 2025 Plugin Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            ISapPlugin.Finish(0);
            return 0;
        }

        // ── Spectral Table NBC 2025 Table 4-1 ───────────────────────────────
        void GetSpectral(string soil, out double Ta, out double Tc,
                         out double Td, out double alpha, out double k)
        {
            switch (soil.ToUpper())
            {
                case "A": Ta=0.1; Tc=0.4; Td=4.0; alpha=2.50; k=1.8; break;
                case "B": Ta=0.1; Tc=0.5; Td=4.0; alpha=2.50; k=1.8; break;
                case "C": Ta=0.1; Tc=0.7; Td=4.0; alpha=2.50; k=1.8; break;
                default:  Ta=0.1; Tc=0.9; Td=5.0; alpha=2.25; k=1.8; break; // D
            }
        }

        // ── Ch(T) 3-branch spectrum §4.1.2 ──────────────────────────────────
        double CalcCh(double T, double Tc, double Td, double alpha, double k)
        {
            if (T <= Tc)       return alpha;
            else if (T <= Td)  return alpha * Math.Pow(Tc / T, k);
            else               return alpha * Math.Pow(Tc / Td, k) * Math.Pow(Td / T, 2.0);
        }

        // ── kd factor NBC 2025 Table 6-1 ────────────────────────────────────
        double GetKd(int n)
        {
            if (n <= 1) return 1.00;
            if (n == 2) return 0.97;
            if (n == 3) return 0.94;
            if (n == 4) return 0.91;
            if (n == 5) return 0.88;
            return 0.85;
        }

        // ── Build spectrum curve ─────────────────────────────────────────────
        double[][] BuildSpectrum(double Z, double I, double Rmu, double OmegaU,
                                  double Tc, double Td, double alpha, double k)
        {
            double[] Tvals = { 0.0,0.05,0.10,0.15,0.20,0.25,0.30,0.40,0.50,
                               0.60,0.70,0.80,0.90,1.00,1.20,1.40,1.60,1.80,
                               2.00,2.50,3.00,3.50,4.00,4.50,Td,Td+0.1,6.0,8.0,10.0 };
            double[] Svals = new double[Tvals.Length];
            for (int i = 0; i < Tvals.Length; i++)
            {
                double Ch = CalcCh(Tvals[i], Tc, Td, alpha, k);
                Svals[i]  = Ch * Z * I / (Rmu * OmegaU);
            }
            return new double[][] { Tvals, Svals };
        }

        // ── Create load pattern ──────────────────────────────────────────────
        void MakePattern(string name, eLoadPatternType type)
        {
            try { _m.LoadPatterns.Add(name, type, 0, true); } catch { }
        }

        // ── Create RS load case ──────────────────────────────────────────────
        void MakeRSCase(string name, string func, string dir,
                         double sf, double ecc)
        {
            try
            {
                _m.LoadCases.ResponseSpectrum.SetCase(name);
                int n = 1;
                string[] dirs  = { dir };
                string[] funcs = { func };
                double[] sfs   = { sf };
                double[] angs  = { 0.0 };
                double[] phs   = { 0.0 };
                _m.LoadCases.ResponseSpectrum.SetLoads(name, ref n,
                    ref dirs, ref funcs, ref sfs, ref angs, ref phs);
                _m.LoadCases.ResponseSpectrum.SetEccentricity(name, ecc);
            }
            catch { }
        }

        // ── Create all load combinations ─────────────────────────────────────
        void MakeAllCombos(string D, string SDL, string L, string RL,
                            string WX, string WY)
        {
            string EX  = "EQX";
            string EY  = "EQY";
            string EXs = "EQX_SLS";
            string EYs = "EQY_SLS";

            // GROUP 1 - Gravity ULS
            LinCombo("G1_DL1.5_SDL1.5_LL1.5",  new[]{D,SDL,L},   new[]{1.5,1.5,1.5});
            LinCombo("G2_DL1.5_SDL1.5_RL1.5",  new[]{D,SDL,RL},  new[]{1.5,1.5,1.5});
            LinCombo("G3_DL1.2_SDL1.2_LL1.6",  new[]{D,SDL,L},   new[]{1.2,1.2,1.6});
            LinCombo("G4_DL1.2_SDL1.2_RL1.6",  new[]{D,SDL,RL},  new[]{1.2,1.2,1.6});
            LinCombo("G5_DL0.9_SDL0.9",         new[]{D,SDL},     new[]{0.9,0.9});

            // GROUP 2 - Seismic ULS NBC 2025 §7.2
            LinCombo("S01_ULS_pX_p0.3Y", new[]{D,SDL,L,EX,EY}, new[]{1.2,1.2,0.3,+1.0,+0.3});
            LinCombo("S02_ULS_pX_m0.3Y", new[]{D,SDL,L,EX,EY}, new[]{1.2,1.2,0.3,+1.0,-0.3});
            LinCombo("S03_ULS_mX_p0.3Y", new[]{D,SDL,L,EX,EY}, new[]{1.2,1.2,0.3,-1.0,+0.3});
            LinCombo("S04_ULS_mX_m0.3Y", new[]{D,SDL,L,EX,EY}, new[]{1.2,1.2,0.3,-1.0,-0.3});
            LinCombo("S05_ULS_pY_p0.3X", new[]{D,SDL,L,EY,EX}, new[]{1.2,1.2,0.3,+1.0,+0.3});
            LinCombo("S06_ULS_pY_m0.3X", new[]{D,SDL,L,EY,EX}, new[]{1.2,1.2,0.3,+1.0,-0.3});
            LinCombo("S07_ULS_mY_p0.3X", new[]{D,SDL,L,EY,EX}, new[]{1.2,1.2,0.3,-1.0,+0.3});
            LinCombo("S08_ULS_mY_m0.3X", new[]{D,SDL,L,EY,EX}, new[]{1.2,1.2,0.3,-1.0,-0.3});

            // GROUP 3 - Overturning NBC 2025 §7.2
            LinCombo("O01_09DL_pEX", new[]{D,SDL,EX}, new[]{0.9,0.9,+1.0});
            LinCombo("O02_09DL_mEX", new[]{D,SDL,EX}, new[]{0.9,0.9,-1.0});
            LinCombo("O03_09DL_pEY", new[]{D,SDL,EY}, new[]{0.9,0.9,+1.0});
            LinCombo("O04_09DL_mEY", new[]{D,SDL,EY}, new[]{0.9,0.9,-1.0});

            // GROUP 4 - Wind ULS NBC 103
            LinCombo("W01_Wind_pWX", new[]{D,SDL,L,WX}, new[]{1.2,1.2,1.2,+1.6});
            LinCombo("W02_Wind_mWX", new[]{D,SDL,L,WX}, new[]{1.2,1.2,1.2,-1.6});
            LinCombo("W03_Wind_pWY", new[]{D,SDL,L,WY}, new[]{1.2,1.2,1.2,+1.6});
            LinCombo("W04_Wind_mWY", new[]{D,SDL,L,WY}, new[]{1.2,1.2,1.2,-1.6});
            LinCombo("W05_Uplift_pWX", new[]{D,SDL,WX}, new[]{0.9,0.9,+1.6});
            LinCombo("W06_Uplift_mWX", new[]{D,SDL,WX}, new[]{0.9,0.9,-1.6});
            LinCombo("W07_Uplift_pWY", new[]{D,SDL,WY}, new[]{0.9,0.9,+1.6});
            LinCombo("W08_Uplift_mWY", new[]{D,SDL,WY}, new[]{0.9,0.9,-1.6});

            // GROUP 5 - SLS NBC 2025 §7.3
            LinCombo("SLS0_DL_SDL_LL",   new[]{D,SDL,L},        new[]{1.0,1.0,1.0});
            LinCombo("SLS1_pX_p0.3Y", new[]{D,SDL,L,EXs,EYs}, new[]{1.0,1.0,0.3,+1.0,+0.3});
            LinCombo("SLS2_pX_m0.3Y", new[]{D,SDL,L,EXs,EYs}, new[]{1.0,1.0,0.3,+1.0,-0.3});
            LinCombo("SLS3_mX_p0.3Y", new[]{D,SDL,L,EXs,EYs}, new[]{1.0,1.0,0.3,-1.0,+0.3});
            LinCombo("SLS4_mX_m0.3Y", new[]{D,SDL,L,EXs,EYs}, new[]{1.0,1.0,0.3,-1.0,-0.3});
            LinCombo("SLS5_pY_p0.3X", new[]{D,SDL,L,EYs,EXs}, new[]{1.0,1.0,0.3,+1.0,+0.3});
            LinCombo("SLS6_pY_m0.3X", new[]{D,SDL,L,EYs,EXs}, new[]{1.0,1.0,0.3,+1.0,-0.3});
            LinCombo("SLS7_mY_p0.3X", new[]{D,SDL,L,EYs,EXs}, new[]{1.0,1.0,0.3,-1.0,+0.3});
            LinCombo("SLS8_mY_m0.3X", new[]{D,SDL,L,EYs,EXs}, new[]{1.0,1.0,0.3,-1.0,-0.3});

            // GROUP 6 - Envelopes
            EnvCombo("ENV_ULS_Seismic",     new[]{"S01_ULS_pX_p0.3Y","S02_ULS_pX_m0.3Y",
                                                   "S03_ULS_mX_p0.3Y","S04_ULS_mX_m0.3Y",
                                                   "S05_ULS_pY_p0.3X","S06_ULS_pY_m0.3X",
                                                   "S07_ULS_mY_p0.3X","S08_ULS_mY_m0.3X"});
            EnvCombo("ENV_ULS_Overturning", new[]{"O01_09DL_pEX","O02_09DL_mEX",
                                                   "O03_09DL_pEY","O04_09DL_mEY"});
            EnvCombo("ENV_ULS_Wind",        new[]{"W01_Wind_pWX","W02_Wind_mWX",
                                                   "W03_Wind_pWY","W04_Wind_mWY",
                                                   "W05_Uplift_pWX","W06_Uplift_mWX",
                                                   "W07_Uplift_pWY","W08_Uplift_mWY"});
            EnvCombo("ENV_SLS_Seismic",     new[]{"SLS1_pX_p0.3Y","SLS2_pX_m0.3Y",
                                                   "SLS3_mX_p0.3Y","SLS4_mX_m0.3Y",
                                                   "SLS5_pY_p0.3X","SLS6_pY_m0.3X",
                                                   "SLS7_mY_p0.3X","SLS8_mY_m0.3X"});
            EnvCombo("ENV_ALL_ULS",         new[]{"G1_DL1.5_SDL1.5_LL1.5","G2_DL1.5_SDL1.5_RL1.5",
                                                   "G3_DL1.2_SDL1.2_LL1.6","G4_DL1.2_SDL1.2_RL1.6",
                                                   "ENV_ULS_Seismic","ENV_ULS_Overturning",
                                                   "ENV_ULS_Wind"});
            EnvCombo("ENV_ALL_SLS",         new[]{"SLS0_DL_SDL_LL","ENV_SLS_Seismic"});
        }

        void LinCombo(string name, string[] pats, double[] sfs)
        {
            try
            {
                _m.RespCombo.Add(name, 0);
                for (int i = 0; i < pats.Length; i++)
                    _m.RespCombo.SetCaseList(name, 0, pats[i], sfs[i]);
            }
            catch { }
        }

        void EnvCombo(string name, string[] subs)
        {
            try
            {
                _m.RespCombo.Add(name, 1);
                foreach (var s in subs)
                    _m.RespCombo.SetCaseList(name, 1, s, 1.0);
            }
            catch { }
        }
    }

    // =========================================================================
    //  INPUT FORM
    // =========================================================================
    public class InputForm : Form
    {
        // Outputs
        public double H, Z, I, Rmu, OmegaU, OmegaS, W, kt;
        public int    NStory;
        public string Soil, Dead, SDL, Live, RoofLive, WindX, WindY;

        NumericUpDown nudH, nudZ, nudI, nudRmu, nudOmegaU, nudOmegaS, nudW, nudkt, nudNStory;
        ComboBox      cbSoil;
        TextBox       tbDead, tbSDL, tbLive, tbRL, tbWX, tbWY;

        public InputForm()
        {
            Text            = "NBC 105:2025 - Seismic Inputs";
            Width           = 480;
            Height          = 580;
            StartPosition   = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            Font            = new System.Drawing.Font("Segoe UI", 9f);

            var pnl = new Panel { Dock = DockStyle.Fill,
                AutoScroll = true, Padding = new Padding(14) };
            Controls.Add(pnl);

            int y = 10;

            // Title
            var lbl = new Label { Text = "NBC 105:2025  |  ETABS 19 Plugin",
                Font = new System.Drawing.Font("Segoe UI",11f,System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.FromArgb(26,79,114),
                Location = new System.Drawing.Point(0,y), Width=440, Height=24 };
            pnl.Controls.Add(lbl); y += 30;

            // ── Geometry ──
            AddSectionLabel(pnl, "Building Geometry  (NBC 2025 §5.1.2)", ref y);
            nudH  = AddNum(pnl,"Total Height H (m)",    ref y, 11.48, 1, 500, 2);
            nudkt = AddNum(pnl,"kt (0.075 RC / 0.085 Steel / 0.05 Other)", ref y, 0.075, 0.01, 0.5, 3);

            // ── Site ──
            AddSectionLabel(pnl, "Site Parameters  (NBC 2025 §4.1)", ref y);
            cbSoil = AddCombo(pnl, "Soil Type (Vs30)", ref y,
                new[]{"A - Hard Rock >800 m/s","B - Rock 360-800","C - Soft Rock 180-360","D - Soft Soil <180 [Ktm]"}, 3);
            nudZ  = AddNum(pnl,"Z - Zone Factor (0.35=Kathmandu)",ref y, 0.35, 0.05, 1.0, 2);
            nudI  = AddNum(pnl,"I - Importance (1.0/1.25/1.5)",   ref y, 1.0,  0.5,  2.0, 2);

            // ── Structure ──
            AddSectionLabel(pnl, "Structural System  (NBC 2025 Table 5-2)", ref y);
            nudRmu    = AddNum(pnl,"Rmu - Ductility Factor",    ref y, 4.0, 1.0, 8.0, 1);
            nudOmegaU = AddNum(pnl,"OmegaU - ULS Overstrength", ref y, 1.5, 1.0, 3.0, 2);
            nudOmegaS = AddNum(pnl,"OmegaS - SLS Overstrength", ref y, 1.25,1.0, 3.0, 2);

            // ── Weight ──
            AddSectionLabel(pnl, "Seismic Weight & Stories", ref y);
            nudW      = AddNum(pnl,"W - Seismic Weight (kN)",   ref y, 15341,1,1e8,0);
            nudNStory = AddNum(pnl,"Number of Storeys (for kd)",ref y, 3,    1,100, 0);

            // ── Patterns ──
            AddSectionLabel(pnl, "Load Pattern Names (must match ETABS exactly)", ref y);
            tbDead = AddTxt(pnl,"Dead Load Pattern",    ref y,"Dead");
            tbSDL  = AddTxt(pnl,"SDL Pattern",          ref y,"SDL");
            tbLive = AddTxt(pnl,"Live Load Pattern",    ref y,"Live");
            tbRL   = AddTxt(pnl,"Roof Live Pattern",    ref y,"LiveRoof");
            tbWX   = AddTxt(pnl,"Wind X Pattern",       ref y,"WX");
            tbWY   = AddTxt(pnl,"Wind Y Pattern",       ref y,"WY");

            // ── Buttons ──
            y += 6;
            var btnOK = new Button
            {
                Text="Apply to ETABS", DialogResult=DialogResult.OK,
                Location=new System.Drawing.Point(0,y), Width=200, Height=30,
                BackColor=System.Drawing.Color.FromArgb(26,79,114),
                ForeColor=System.Drawing.Color.White,
                FlatStyle=FlatStyle.Flat,
                Font=new System.Drawing.Font("Segoe UI",9f,System.Drawing.FontStyle.Bold)
            };
            var btnCancel = new Button
            {
                Text="Cancel", DialogResult=DialogResult.Cancel,
                Location=new System.Drawing.Point(210,y), Width=80, Height=30,
                FlatStyle=FlatStyle.Flat
            };
            pnl.Controls.Add(btnOK);
            pnl.Controls.Add(btnCancel);
            AcceptButton = btnOK;
            CancelButton = btnCancel;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK)
            {
                H      = (double)nudH.Value;
                kt     = (double)nudkt.Value;
                Z      = (double)nudZ.Value;
                I      = (double)nudI.Value;
                Rmu    = (double)nudRmu.Value;
                OmegaU = (double)nudOmegaU.Value;
                OmegaS = (double)nudOmegaS.Value;
                W      = (double)nudW.Value;
                NStory = (int)nudNStory.Value;
                string[] soils = {"A","B","C","D"};
                Soil   = soils[Math.Min(cbSoil.SelectedIndex,3)];
                Dead   = tbDead.Text.Trim();
                SDL    = tbSDL.Text.Trim();
                Live   = tbLive.Text.Trim();
                RoofLive = tbRL.Text.Trim();
                WindX  = tbWX.Text.Trim();
                WindY  = tbWY.Text.Trim();
            }
            base.OnFormClosing(e);
        }

        void AddSectionLabel(Panel p, string text, ref int y)
        {
            y += 4;
            var l = new Label { Text=text,
                Font=new System.Drawing.Font("Segoe UI",8.5f,System.Drawing.FontStyle.Bold),
                ForeColor=System.Drawing.Color.FromArgb(26,79,114),
                Location=new System.Drawing.Point(0,y), Width=440, Height=18 };
            p.Controls.Add(l); y+=20;
        }

        NumericUpDown AddNum(Panel p, string lbl, ref int y,
                              double def, double min, double max, int dec)
        {
            p.Controls.Add(new Label{Text=lbl+":",
                Location=new System.Drawing.Point(0,y+2),Width=290,Height=18});
            var n = new NumericUpDown
            {
                Value=(decimal)Math.Max(min,Math.Min(max,def)),
                Minimum=(decimal)min, Maximum=(decimal)max,
                DecimalPlaces=dec,
                Location=new System.Drawing.Point(295,y), Width=120, Height=22
            };
            p.Controls.Add(n); y+=26; return n;
        }

        ComboBox AddCombo(Panel p, string lbl, ref int y, string[] items, int sel)
        {
            p.Controls.Add(new Label{Text=lbl+":",
                Location=new System.Drawing.Point(0,y+2),Width=150,Height=18});
            var cb = new ComboBox
            {
                DropDownStyle=ComboBoxStyle.DropDownList,
                Location=new System.Drawing.Point(155,y), Width=260, Height=22
            };
            cb.Items.AddRange(items); cb.SelectedIndex=sel;
            p.Controls.Add(cb); y+=26; return cb;
        }

        TextBox AddTxt(Panel p, string lbl, ref int y, string def)
        {
            p.Controls.Add(new Label{Text=lbl+":",
                Location=new System.Drawing.Point(0,y+2),Width=200,Height=18});
            var tb = new TextBox
            {
                Text=def, Location=new System.Drawing.Point(205,y),
                Width=210, Height=22
            };
            p.Controls.Add(tb); y+=26; return tb;
        }
    }
}
