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
                MessageBox.Show("Could not connect to ETABS 19. Make sure ETABS 19 is open. Error: " + ex.Message, "NBC 2025 - Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

                _m.SetPresentUnits(eUnits.kN_m_C);

                MakePat("EQX",     eLoadPatternType.Quake);
                MakePat("EQY",     eLoadPatternType.Quake);
                MakePat("EQX_SLS", eLoadPatternType.Quake);
                MakePat("EQY_SLS", eLoadPatternType.Quake);

                MakeAllCombos(D, SDL, L, RL, WX, WY);

                string nl  = Environment.NewLine;
                string msg = "NBC 105:2025 Plugin Complete!" + nl + nl;
                msg += "TIME PERIOD" + nl;
                msg += "T1 = " + T1.ToString("F4") + " s" + nl;
                msg += "T_design = " + Tdes.ToString("F4") + " s" + nl + nl;
                msg += "SITE SPECTRA" + nl;
                msg += "Soil=" + soil + "  Tc=" + Tc.ToString("F1") + "  Td=" + Td.ToString("F1") + nl;
                msg += "Ch(T) = " + Ch.ToString("F4") + nl;
                msg += "C(T)  = " + CT.ToString("F4") + nl;
                msg += "Cs(T) = " + CsT.ToString("F4") + nl + nl;
                msg += "DESIGN COEFFICIENTS" + nl;
                msg += "Cd_ULS = " + CdULS.ToString("F5") + nl;
                msg += "Cd_SLS = " + CdSLS.ToString("F5") + nl;
                msg += "kd     = " + kd.ToString("F2") + nl + nl;
                msg += "BASE SHEAR" + nl;
                msg += "V_ULS = " + VULS.ToString("F1") + " kN" + nl;
                msg += "V_SLS = " + VSLS.ToString("F1") + " kN" + nl + nl;
                msg += "CREATED IN ETABS" + nl;
                msg += "Patterns : EQX EQY EQX_SLS EQY_SLS" + nl;
                msg += "Combos   : 40 combinations" + nl + nl;
                msg += "Please re-run analysis.";
                MessageBox.Show(msg, "NBC 105:2025 - Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "NBC 2025 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            LC("S07_ULS_mY_p03X", new string[]{D,SDL,L,EY,EX}, new double[]{1.2,1.2,0.3,-1.0,+0.3});
            LC("S08_ULS_mY_m03X", new string[]{D,SDL,L,EY,EX}, new double[]{1.2,1.2,0.3,-1.0,-0.3});

            LC("O01_09DL_pEX", new string[]{D,SDL,EX}, new double[]{0.9,0.9,+1.0});
            LC("O02_09DL_mEX", new string[]{D,SDL,EX}, new double[]{0.9,0.9,-1.0});
            LC("O03_09DL_pEY", new string[]{D,SDL,EY}, new double[]{0.9,0.9,+1.0});
            LC("O04_09DL_mEY", new string[]{D,SDL,EY}, new double[]{0.9,0.9,-1.0});

            LC("W01_Wind_pWX",   new string[]{D,SDL,L,WX}, new double[]{1.2,1.2,1.2,+1.6});
            LC("W02_Wind_mWX",   new string[]{D,SDL,L,WX}, new double[]{1.2,1.2,1.2,-1.6});
            LC("W03_Wind_pWY",   new string[]{D,SDL,L,WY}, new double[]{1.2,1.2,1.2,+1.6});
            LC("W04_Wind_mWY",   new string[]{D,SDL,L,WY}, new double[]{1.2,1.2,1.2,-1.6});
            LC("W05_Uplift_pWX", new string[]{D,SDL,WX},   new double[]{0.9,0.9,+1.6});
            LC("W06_Uplift_mWX", new string[]{D,SDL,WX},   new double[]{0.9,0.9,-1.6});
            LC("W07_Uplift_pWY", new string[]{D,SDL,WY},   new double[]{0.9,0.9,+1.6});
            LC("W08_Uplift_mWY", new string[]{D,SDL,WY},   new double[]{0.9,0.9,-1.6});

            LC("SLS0_DL_SDL_LL",  new string[]{D,SDL,L},         new double[]{1.0,1.0,1.0});
            LC("SLS1_pX_p03Y", new string[]{D,SDL,L,EXs,EYs}, new double[]{1.0,1.0,0.3,+1.0,+0.3});
            LC("SLS2_pX_m03Y", new string[]{D,SDL,L,EXs,EYs}, new double[]{1.0,1.0,0.3,+1.0,-0.3});
            LC("SLS3_mX_p03Y", new string[]{D,SDL,L,EXs,EYs}, new double[]{1.0,1.0,0.3,-1.0,+0.3});
            LC("SLS4_mX_m03Y", new string[]{D,SDL,L,EXs,EYs}, new double[]{1.0,1.0,0.3,-1.0,-0.3});
            LC("SLS5_pY_p03X", new string[]{D,SDL,L,EYs,EXs}, new double[]{1.0,1.0,0.3,+1.0,+0.3});
            LC("SLS6_pY_m03X", new string[]{D,SDL,L,EYs,EXs}, new double[]{1.0,1.0,0.3,+1.0,-0.3});
            LC("SLS7_mY_p03X", new string[]{D,SDL,L,EYs,EXs}, new double[]{1.0,1.0,0.3,-1.0,+0.3});
            LC("SLS8_mY_m03X", new string[]{D,SDL,L,EYs,EXs}, new double[]{1.0,1.0,0.3,-1.0,-0.3});

            EC("ENV_ULS_Seismic",     new string[]{"S01_ULS_pX_p03Y","S02_ULS_pX_m03Y","S03_ULS_mX_p03Y","S04_ULS_mX_m03Y","S05_ULS_pY_p03X","S06_ULS_pY_m03X","S07_ULS_mY_p03X","S08_ULS_mY_m03X"});
            EC("ENV_ULS_Overturning", new string[]{"O01_09DL_pEX","O02_09DL_mEX","O03_09DL_pEY","O04_09DL_mEY"});
            EC("ENV_ULS_Wind",        new string[]{"W01_Wind_pWX","W02_Wind_mWX","W03_Wind_pWY","W04_Wind_mWY","W05_Uplift_pWX","W06_Uplift_mWX","W07_Uplift_pWY","W08_Uplift_mWY"});
            EC("ENV_SLS_Seismic",     new string[]{"SLS1_pX_p03Y","SLS2_pX_m03Y","SLS3_mX_p03Y","SLS4_mX_m03Y","SLS5_pY_p03X","SLS6_pY_m03X","SLS7_mY_p03X","SLS8_mY_m03X"});
            EC("ENV_ALL_ULS",         new string[]{"G1_DL1.5_SDL1.5_LL1.5","G2_DL1.5_SDL1.5_RL1.5","G3_DL1.2_SDL1.2_LL1.6","G4_DL1.2_SDL1.2_RL1.6","ENV_ULS_Seismic","ENV_ULS_Overturning","ENV_ULS_Wind"});
            EC("ENV_ALL_SLS",         new string[]{"SLS0_DL_SDL_LL","ENV_SLS_Seismic"});
        }

        void LC(string name, string[] pats, double[] sfs)
        {
            try
            {
                _m.RespCombo.Add(name, 0);
                eCNameType cType = eCNameType.LoadCase;
                for (int i=0; i<pats.Length; i++)
                    _m.RespCombo.SetCaseList(name, ref cType, pats[i], sfs[i]);
            }
            catch {}
        }

        void EC(string name, string[] subs)
        {
            try
            {
                _m.RespCombo.Add(name, 1);
                eCNameType cType = eCNameType.LoadCombo;
                for (int i=0; i<subs.Length; i++)
                    _m.RespCombo.SetCaseList(name, ref cType, subs[i], 1.0);
            }
            catch {}
        }
    }

    public class InputForm : Form
    {
        public double H,Z,I,Rmu,OmegaU,OmegaS,W,kt;
        public int    NStory;
        public string Soil,Dead,SDL,Live,RoofLive,WindX,WindY;

        NumericUpDown nudH,nudZ,nudI,nudRmu,nudOmegaU,nudOmegaS,nudW,nudkt,nudNStory;
        ComboBox cbSoil;
        TextBox  tbDead,tbSDL,tbLive,tbRL,tbWX,tbWY;

        public InputForm()
        {
            Text            = "NBC 105:2025 - Seismic Inputs";
            Width           = 480;
            Height          = 600;
            StartPosition   = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            Font            = new System.Drawing.Font("Segoe UI", 9f);

            Panel pnl = new Panel();
            pnl.Dock       = DockStyle.Fill;
            pnl.AutoScroll = true;
            pnl.Padding    = new Padding(14);
            Controls.Add(pnl);
            int y = 10;

            Label title = new Label();
            title.Text      = "NBC 105:2025  |  ETABS 19 Plugin";
            title.Font      = new System.Drawing.Font("Segoe UI",11f,System.Drawing.FontStyle.Bold);
            title.ForeColor = System.Drawing.Color.FromArgb(26,79,114);
            title.Location  = new System.Drawing.Point(0,y);
            title.Width     = 440;
            title.Height    = 24;
            pnl.Controls.Add(title);
            y += 32;

            Sec(pnl,"Building Geometry",ref y);
            nudH  = Num(pnl,"Total Height H (m)",         ref y,11.48,1,500,2);
            nudkt = Num(pnl,"kt (RC=0.075  Steel=0.085)", ref y,0.075,0.01,0.5,3);

            Sec(pnl,"Site Parameters",ref y);
            cbSoil = Cmb(pnl,"Soil Type (Vs30)",ref y,
                new string[]{"A - Hard Rock  >800 m/s",
                             "B - Rock  360-800 m/s",
                             "C - Soft Rock  180-360 m/s",
                             "D - Soft Soil  <180 m/s  [Kathmandu]"},3);
            nudZ = Num(pnl,"Z - Zone Factor (0.35=Kathmandu)",  ref y,0.35,0.05,1.0,2);
            nudI = Num(pnl,"I - Importance (1.0 / 1.25 / 1.5)", ref y,1.0,0.5,2.0,2);

            Sec(pnl,"Structural System",ref y);
            nudRmu    = Num(pnl,"Rmu - Ductility Factor",    ref y,4.0,1.0,8.0,1);
            nudOmegaU = Num(pnl,"OmegaU - ULS Overstrength", ref y,1.5,1.0,3.0,2);
            nudOmegaS = Num(pnl,"OmegaS - SLS Overstrength", ref y,1.25,1.0,3.0,2);

            Sec(pnl,"Seismic Weight and Stories",ref y);
            nudW      = Num(pnl,"W - Seismic Weight (kN)",   ref y,15341,1,99999999,0);
            nudNStory = Num(pnl,"Number of Storeys",          ref y,3,1,100,0);

            Sec(pnl,"Load Pattern Names",ref y);
            tbDead = Txt(pnl,"Dead Load", ref y,"Dead");
            tbSDL  = Txt(pnl,"SDL",       ref y,"SDL");
            tbLive = Txt(pnl,"Live Load", ref y,"Live");
            tbRL   = Txt(pnl,"Roof Live", ref y,"LiveRoof");
            tbWX   = Txt(pnl,"Wind X",    ref y,"WX");
            tbWY   = Txt(pnl,"Wind Y",    ref y,"WY");

            y += 8;
            Button ok = new Button();
            ok.Text         = "Apply to ETABS";
            ok.DialogResult = DialogResult.OK;
            ok.Location     = new System.Drawing.Point(0,y);
            ok.Width        = 200;
            ok.Height       = 30;
            ok.BackColor    = System.Drawing.Color.FromArgb(26,79,114);
            ok.ForeColor    = System.Drawing.Color.White;
            ok.FlatStyle    = FlatStyle.Flat;
            ok.Font         = new System.Drawing.Font("Segoe UI",9f,System.Drawing.FontStyle.Bold);

            Button cn = new Button();
            cn.Text         = "Cancel";
            cn.DialogResult = DialogResult.Cancel;
            cn.Location     = new System.Drawing.Point(210,y);
            cn.Width        = 80;
            cn.Height       = 30;
            cn.FlatStyle    = FlatStyle.Flat;

            pnl.Controls.Add(ok);
            pnl.Controls.Add(cn);
            AcceptButton = ok;
            CancelButton = cn;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK)
            {
                H        = (double)nudH.Value;
                kt       = (double)nudkt.Value;
                Z        = (double)nudZ.Value;
                I        = (double)nudI.Value;
                Rmu      = (double)nudRmu.Value;
                OmegaU   = (double)nudOmegaU.Value;
                OmegaS   = (double)nudOmegaS.Value;
                W        = (double)nudW.Value;
                NStory   = (int)nudNStory.Value;
                string[] ss = new string[]{"A","B","C","D"};
                Soil     = ss[Math.Min(cbSoil.SelectedIndex,3)];
                Dead     = tbDead.Text.Trim();
                SDL      = tbSDL.Text.Trim();
                Live     = tbLive.Text.Trim();
                RoofLive = tbRL.Text.Trim();
                WindX    = tbWX.Text.Trim();
                WindY    = tbWY.Text.Trim();
            }
            base.OnFormClosing(e);
        }

        void Sec(Panel p, string t, ref int y)
        {
            y += 4;
            Label l = new Label();
            l.Text      = t;
            l.Font      = new System.Drawing.Font("Segoe UI",8.5f,System.Drawing.FontStyle.Bold);
            l.ForeColor = System.Drawing.Color.FromArgb(26,79,114);
            l.Location  = new System.Drawing.Point(0,y);
            l.Width     = 440;
            l.Height    = 18;
            p.Controls.Add(l);
            y += 20;
        }

        NumericUpDown Num(Panel p, string l, ref int y,
                          double d, double mn, double mx, int dc)
        {
            Label lbl = new Label();
            lbl.Text     = l + ":";
            lbl.Location = new System.Drawing.Point(0,y+2);
            lbl.Width    = 290;
            lbl.Height   = 18;
            p.Controls.Add(lbl);
            NumericUpDown n = new NumericUpDown();
            n.Value         = (decimal)Math.Max(mn,Math.Min(mx,d));
            n.Minimum       = (decimal)mn;
            n.Maximum       = (decimal)mx;
            n.DecimalPlaces = dc;
            n.Location      = new System.Drawing.Point(295,y);
            n.Width         = 120;
            n.Height        = 22;
            p.Controls.Add(n);
            y += 26;
            return n;
        }

        ComboBox Cmb(Panel p, string l, ref int y, string[] its, int sel)
        {
            Label lbl = new Label();
            lbl.Text     = l + ":";
            lbl.Location = new System.Drawing.Point(0,y+2);
            lbl.Width    = 150;
            lbl.Height   = 18;
            p.Controls.Add(lbl);
            ComboBox cb = new ComboBox();
            cb.DropDownStyle = ComboBoxStyle.DropDownList;
            cb.Location      = new System.Drawing.Point(155,y);
            cb.Width         = 260;
            cb.Height        = 22;
            for (int i=0;i<its.Length;i++) cb.Items.Add(its[i]);
            cb.SelectedIndex = sel;
            p.Controls.Add(cb);
            y += 26;
            return cb;
        }

        TextBox Txt(Panel p, string l, ref int y, string d)
        {
            Label lbl = new Label();
            lbl.Text     = l + ":";
            lbl.Location = new System.Drawing.Point(0,y+2);
            lbl.Width    = 200;
            lbl.Height   = 18;
            p.Controls.Add(lbl);
            TextBox tb = new TextBox();
            tb.Text     = d;
            tb.Location = new System.Drawing.Point(205,y);
            tb.Width    = 210;
            tb.Height   = 22;
            p.Controls.Add(tb);
            y += 26;
            return tb;
        }
    }
}
