using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoreLinq;
using VolvoWrench.Demo_stuff.GoldSource;

namespace VolvoWrench.Demo_Stuff.GoldSource
{
    /// <summary>
    ///     This is a form to aid goldsource run verification
    /// </summary>
    public sealed partial class Verification : Form
    {
        public static Dictionary<string, CrossParseResult> Df = new Dictionary<string, CrossParseResult>();

        /// <summary>
        ///     This list contains the paths to the demos
        /// </summary>
        public List<string> DemopathList;

        /// <summary>
        ///     Default constructor
        /// </summary>
        public Verification()
        {
            InitializeComponent();
            DemopathList = new List<string>();
            this.mrtb.DragDrop += Verification_DragDrop;
            this.mrtb.DragEnter += Verification_DragEnter;
            this.mrtb.AllowDrop = true;
            AllowDrop = true;
        }

        private void openDemosToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var of = new OpenFileDialog
            {
                Filter = @"Demo files (.dem) | *.dem",
                Multiselect = true
            };

            if (of.ShowDialog() == DialogResult.OK)
            {
                Verify(of.FileNames);
            }
            else
            {
                mrtb.Text = @"No file selected/bad file selected!";
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void demostartCommandToClipboardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (DemopathList.ToArray().Length <= 32)
                Clipboard.SetText("startdemos " + DemopathList
                    .Select(Path.GetFileNameWithoutExtension)
                    .OrderBy(x => int.Parse(Regex.Match(x + "0", @"\d+").Value))
                    .ToList()
                    .Aggregate((c, n) => c + " " + n));
            else
            {
                Clipboard.SetText(DemopathList
                    .Select(Path.GetFileNameWithoutExtension)
                    .OrderBy(x => int.Parse(Regex.Match(x + "0", @"\d+").Value))
                    .Batch(32)
                    .Aggregate(string.Empty, (x, y) => x + ";startdemos " + (y.Aggregate((c, n) => c + " " + n)))
                    .Substring(1));
            }
            using (var ni = new NotifyIcon())
            {
                ni.Icon = SystemIcons.Exclamation;
                ni.Visible = true;
                ni.ShowBalloonTip(5000, "VolvoWrench", "Demo names copied to clipboard", ToolTipIcon.Info);
            }
        }

        /// <summary>
        ///     This is the actuall verification method
        /// </summary>
        /// <param name="files">The paths of the files</param>
        public void Verify(string[] files)
        {
            Df.Clear();
            mrtb.Text = $@"Please wait. Parsing demos... 0/{files.Length}";
            mrtb.Invalidate();
            mrtb.Update();
            mrtb.Refresh();
            Application.DoEvents();
            var curr = 0;
            foreach (var dt in files.Where(file => File.Exists(file) && Path.GetExtension(file) == ".dem"))
            {
                DemopathList.Add(dt);
                Df.Add(dt, CrossDemoParser.Parse(dt)); //If someone bothers me that its slow make it async.
                mrtb.Text = $@"Please wait. Parsing demos... {curr++}/{files.Length}";
                mrtb.Invalidate();
                mrtb.Update();
                mrtb.Refresh();
                Application.DoEvents();
            }
            if (Df.Any(x => x.Value.GsDemoInfo.ParsingErrors.Count > 0))
            {
                var brokendemos = Df.Where(x => x.Value.GsDemoInfo.ParsingErrors.Count > 0)
                    .ToList()
                    .Aggregate("", (c, n) => c += "\n" + n.Key);
                MessageBox.Show(@"Broken demos found:
" + brokendemos, @"Error!", MessageBoxButtons.OK);
                Main.Log("Broken demos when verification: " + brokendemos);
                mrtb.Text = @"Please fix the demos then reselect the files!";
                return;
            }
            if (Df.Any(x => x.Value.Type != Parseresult.GoldSource))
                MessageBox.Show(@"Only goldsource supported");
            else
            {
                mrtb.Text = "";
                mrtb.AppendText("" + "\n");
                mrtb.AppendText("Parsed demos. Results:" + "\n");
                mrtb.AppendText("General stats:" + "\n");
                mrtb.AppendText($@"
Highest FPS:                {(1/Df.Select(x => x.Value).ToList().Min(y => y.GsDemoInfo.AditionalStats.FrametimeMin)).ToString("N2")}
Lowest FPS:                 {(1/Df.Select(x => x.Value).ToList().Max(y => y.GsDemoInfo.AditionalStats.FrametimeMax)).ToString("N2")}
Average FPS:                {(Df.Select(z => z.Value).ToList().Average(k => k.GsDemoInfo.AditionalStats.Count/k.GsDemoInfo.AditionalStats.FrametimeSum)).ToString("N2")}
Lowest msec:                {(1000.0/Df.Select(x => x.Value).ToList().Min(y => y.GsDemoInfo.AditionalStats.MsecMin)).ToString("N2")} FPS
Highest msec:               {(1000.0/Df.Select(x => x.Value).ToList().Max(y => y.GsDemoInfo.AditionalStats.MsecMax)).ToString("N2")} FPS
Average msec:               {(Df.Select(x => x.Value).ToList().Average(y => y.GsDemoInfo.AditionalStats.MsecSum/(double) y.GsDemoInfo.AditionalStats.Count)).ToString("N2")} FPS

Total time of the demos:    {Df.Sum(x => x.Value.GsDemoInfo.DirectoryEntries.Sum(y => y.TrackTime))}s
Human readable time:        {TimeSpan.FromSeconds(Df.Sum(x => x.Value.GsDemoInfo.DirectoryEntries.Sum(y => y.TrackTime))).ToString("g")}" + "\n\n");

                mrtb.AppendText("Demo cheat check:" + "\n");
                foreach (var dem in Df)
                {
                    if (dem.Value.GsDemoInfo.Cheats.Count > 0)
                    {
                        mrtb.AppendText("Possible cheats:\n");
                        foreach (var cheat in dem.Value.GsDemoInfo.Cheats.Distinct())
                        {
                            mrtb.AppendText("\t" + cheat + "\n");
                        }
                    }
                    mrtb.AppendText(Path.GetFileName(dem.Key) + " -> " + dem.Value.GsDemoInfo.Header.MapName);
                    mrtb.AppendText("\nBXTData:");
                    mrtb.AppendText("\n" + ParseBxtData(dem));
                }
            }
        }

        /// <summary>
        /// Parses the bxt data into treenodes
        /// </summary>
        /// <param name="Infos"></param>
        public string ParseBxtData(KeyValuePair<string, CrossParseResult> info)
        {
            string ret = "\n";
            const string bxtVersion = "29290999b3607f61cee4b8e65c4faa90cff4afa0-CLEAN based on aug-1-2020";
            var cvarRules = new Dictionary<string, string>()
            {
                {"BXT_AUTOJUMP", "0"},
                {"BXT_BHOPCAP", "0"},
                {"BXT_COLLISION_DEPTH_MAP", "0"},
                {"BXT_FADE_REMOVE", "0"},
                {"BXT_HUD_DISTANCE", "0"},
                {"BXT_HUD_ENTITY_HP", "0"},
                {"BXT_HUD_ENTITY_INFO", "0"},
                {"BXT_HUD_ENTITIES", "0"},
                {"BXT_HUD_HEALTH", "0"},
                {"BXT_HUD_ORIGIN", "0"},
                {"BXT_HUD_SELFGAUSS","0"},
                {"BXT_HUD_USEABLES", "0"},
                {"BXT_HUD_NIHILANTH", "0"},
                {"BXT_HUD_VELOCITY", "0"},
                {"BXT_HUD_VISIBLE_LANDMARKS", "0"},
                {"BXT_NOVIS", "0"},
                {"BXT_SHOW_HIDDEN_ENTITIES", "0"},
                {"BXT_SHOW_PICKUP_BBOX", "0"},
                {"BXT_SHOW_TRIGGERS", "0"},
                {"BXT_SHOW_TRIGGERS_LEGACY", "0"},
                {"BXT_WALLHACK", "0"},
                {"CHASE_ACTIVE", "0"},
                {"CL_ANGLESPEEDKEY", "0.67"},
                {"CL_BACKSPEED", "400"},
                {"CL_FORWARDSPEED", "400"},
                {"CL_PITCHDOWN", "89"},
                {"CL_PITCHSPEED", "225"},
                {"CL_PITCHUP", "89"},
                {"CL_SIDESPEED", "400"},
                {"CL_UPSPEED", "320"},
                {"CL_YAWSPEED", "210"},
                {"GL_MONOLIGHTS", "0"},
                {"HOST_FRAMERATE", "0"},
                {"HOST_SPEEDS", "0"},
                {"R_DRAWENTITIES", "1"},
                {"R_FULLBRIGHT", "0"},
                {"SK_12MM_BULLET1", "8"},
                {"SK_12MM_BULLET2", "10"},
                {"SK_12MM_BULLET3", "10"},
                {"SK_9MM_BULLET1", "5"},
                {"SK_9MM_BULLET2", "5"},
                {"SK_9MM_BULLET3", "8"},
                {"SK_9MMAR_BULLET1", "3"},
                {"SK_9MMAR_BULLET2", "4"},
                {"SK_9MMAR_BULLET3", "5"},
                {"SK_AGRUNT_DMG_PUNCH1", "10"},
                {"SK_AGRUNT_DMG_PUNCH2", "20"},
                {"SK_AGRUNT_DMG_PUNCH3", "20"},
                {"SK_AGRUNT_HEALTH1", "60"},
                {"SK_AGRUNT_HEALTH2", "90"},
                {"SK_AGRUNT_HEALTH3", "120"},
                {"SK_APACHE_HEALTH1", "150"},
                {"SK_APACHE_HEALTH2", "250"},
                {"SK_APACHE_HEALTH3", "400"},
                {"SK_BARNEY_HEALTH1", "35"},
                {"SK_BARNEY_HEALTH2", "35"},
                {"SK_BARNEY_HEALTH3", "35"},
                {"SK_BATTERY1", "15"},
                {"SK_BATTERY2", "15"},
                {"SK_BATTERY3", "10"},
                {"SK_BIGMOMMA_DMG_BLAST1", "100"},
                {"SK_BIGMOMMA_DMG_BLAST2", "120"},
                {"SK_BIGMOMMA_DMG_BLAST3", "160"},
                {"SK_BIGMOMMA_DMG_SLASH1", "50"},
                {"SK_BIGMOMMA_DMG_SLASH2", "60"},
                {"SK_BIGMOMMA_DMG_SLASH3", "70"},
                {"SK_BIGMOMMA_HEALTH_FACTOR1", "1.0"},
                {"SK_BIGMOMMA_HEALTH_FACTOR2", "1.5"},
                {"SK_BIGMOMMA_HEALTH_FACTOR3", "2"},
                {"SK_BIGMOMMA_RADIUS_BLAST1", "250"},
                {"SK_BIGMOMMA_RADIUS_BLAST2", "250"},
                {"SK_BIGMOMMA_RADIUS_BLAST3", "275"},
                {"SK_BULLSQUID_DMG_BITE1", "15"},
                {"SK_BULLSQUID_DMG_BITE2", "25"},
                {"SK_BULLSQUID_DMG_BITE3", "25"},
                {"SK_BULLSQUID_DMG_SPIT1", "10"},
                {"SK_BULLSQUID_DMG_SPIT2", "10"},
                {"SK_BULLSQUID_DMG_SPIT3", "15"},
                {"SK_BULLSQUID_DMG_WHIP1", "25"},
                {"SK_BULLSQUID_DMG_WHIP2", "35"},
                {"SK_BULLSQUID_DMG_WHIP3", "35"},
                {"SK_BULLSQUID_HEALTH1", "40"},
                {"SK_BULLSQUID_HEALTH2", "40"},
                {"SK_BULLSQUID_HEALTH3", "120"},
                {"SK_CONTROLLER_DMGBALL1", "3"},
                {"SK_CONTROLLER_DMGBALL2", "4"},
                {"SK_CONTROLLER_DMGBALL3", "5"},
                {"SK_CONTROLLER_DMGZAP1", "15"},
                {"SK_CONTROLLER_DMGZAP2", "25"},
                {"SK_CONTROLLER_DMGZAP3", "35"},
                {"SK_CONTROLLER_HEALTH1", "60"},
                {"SK_CONTROLLER_HEALTH2", "60"},
                {"SK_CONTROLLER_HEALTH3", "100"},
                {"SK_CONTROLLER_SPEEDBALL1", "650"},
                {"SK_CONTROLLER_SPEEDBALL2", "800"},
                {"SK_CONTROLLER_SPEEDBALL3", "1000"},
                {"SK_GARGANTUA_DMG_FIRE1", "3"},
                {"SK_GARGANTUA_DMG_FIRE2", "5"},
                {"SK_GARGANTUA_DMG_FIRE3", "5"},
                {"SK_GARGANTUA_DMG_SLASH1", "10"},
                {"SK_GARGANTUA_DMG_SLASH2", "30"},
                {"SK_GARGANTUA_DMG_SLASH3", "30"},
                {"SK_GARGANTUA_DMG_STOMP1", "50"},
                {"SK_GARGANTUA_DMG_STOMP2", "100"},
                {"SK_GARGANTUA_DMG_STOMP3", "100"},
                {"SK_GARGANTUA_HEALTH1", "800"},
                {"SK_GARGANTUA_HEALTH2", "800"},
                {"SK_GARGANTUA_HEALTH3", "1000"},
                {"SK_HASSASSIN_HEALTH1", "30"},
                {"SK_HASSASSIN_HEALTH2", "50"},
                {"SK_HASSASSIN_HEALTH3", "50"},
                {"SK_HEADCRAB_DMG_BITE1", "5"},
                {"SK_HEADCRAB_DMG_BITE2", "10"},
                {"SK_HEADCRAB_DMG_BITE3", "10"},
                {"SK_HEADCRAB_HEALTH1", "10"},
                {"SK_HEADCRAB_HEALTH2", "10"},
                {"SK_HEADCRAB_HEALTH3", "20"},
                {"SK_HEALTHCHARGER1", "50"},
                {"SK_HEALTHCHARGER2", "40"},
                {"SK_HEALTHCHARGER3", "25"},
                {"SK_HEALTHKIT1", "15"},
                {"SK_HEALTHKIT2", "15"},
                {"SK_HEALTHKIT3", "10"},
                {"SK_HGRUNT_GSPEED1", "400"},
                {"SK_HGRUNT_GSPEED2", "600"},
                {"SK_HGRUNT_GSPEED3", "800"},
                {"SK_HGRUNT_HEALTH1", "50"},
                {"SK_HGRUNT_HEALTH2", "50"},
                {"SK_HGRUNT_HEALTH3", "80"},
                {"SK_HGRUNT_KICK1", "5"},
                {"SK_HGRUNT_KICK2", "10"},
                {"SK_HGRUNT_KICK3", "10"},
                {"SK_HGRUNT_PELLETS1", "3"},
                {"SK_HGRUNT_PELLETS2", "5"},
                {"SK_HGRUNT_PELLETS3", "6"},
                {"SK_HORNET_DMG1", "4"},
                {"SK_HORNET_DMG2", "5"},
                {"SK_HORNET_DMG3", "8"},
                {"SK_HOUNDEYE_DMG_BLAST1", "10"},
                {"SK_HOUNDEYE_DMG_BLAST2", "15"},
                {"SK_HOUNDEYE_DMG_BLAST3", "15"},
                {"SK_HOUNDEYE_HEALTH1", "20"},
                {"SK_HOUNDEYE_HEALTH2", "20"},
                {"SK_HOUNDEYE_HEALTH3", "30"},
                {"SK_ICHTHYOSAUR_HEALTH1", "200"},
                {"SK_ICHTHYOSAUR_HEALTH2", "200"},
                {"SK_ICHTHYOSAUR_HEALTH3", "400"},
                {"SK_ICHTHYOSAUR_SHAKE1", "20"},
                {"SK_ICHTHYOSAUR_SHAKE2", "35"},
                {"SK_ICHTHYOSAUR_SHAKE3", "50"},
                {"SK_ISLAVE_DMG_CLAW1", "8"},
                {"SK_ISLAVE_DMG_CLAW2", "10"},
                {"SK_ISLAVE_DMG_CLAW3", "10"},
                {"SK_ISLAVE_DMG_CLAWRAKE1", "25"},
                {"SK_ISLAVE_DMG_CLAWRAKE2", "25"},
                {"SK_ISLAVE_DMG_CLAWRAKE3", "25"},
                {"SK_ISLAVE_DMG_ZAP1", "10"},
                {"SK_ISLAVE_DMG_ZAP2", "10"},
                {"SK_ISLAVE_DMG_ZAP3", "15"},
                {"SK_ISLAVE_HEALTH1", "30"},
                {"SK_ISLAVE_HEALTH2", "30"},
                {"SK_ISLAVE_HEALTH3", "60"},
                {"SK_LEECH_DMG_BITE1", "2"},
                {"SK_LEECH_DMG_BITE2", "2"},
                {"SK_LEECH_DMG_BITE3", "2"},
                {"SK_LEECH_HEALTH1", "2"},
                {"SK_LEECH_HEALTH2", "2"},
                {"SK_LEECH_HEALTH3", "2"},
                {"SK_MINITURRET_HEALTH1", "40"},
                {"SK_MINITURRET_HEALTH2", "40"},
                {"SK_MINITURRET_HEALTH3", "50"},
                {"SK_MONSTER_ARM1", "1"},
                {"SK_MONSTER_ARM2", "1"},
                {"SK_MONSTER_ARM3", "1"},
                {"SK_MONSTER_CHEST1", "1"},
                {"SK_MONSTER_CHEST2", "1"},
                {"SK_MONSTER_CHEST3", "1"},
                {"SK_MONSTER_HEAD1", "3"},
                {"SK_MONSTER_HEAD2", "3"},
                {"SK_MONSTER_HEAD3", "3"},
                {"SK_MONSTER_LEG1", "1"},
                {"SK_MONSTER_LEG2", "1"},
                {"SK_MONSTER_LEG3", "1"},
                {"SK_MONSTER_STOMACH1", "1"},
                {"SK_MONSTER_STOMACH2", "1"},
                {"SK_MONSTER_STOMACH3", "1"},
                {"SK_NIHILANTH_HEALTH1", "800"},
                {"SK_NIHILANTH_HEALTH2", "800"},
                {"SK_NIHILANTH_HEALTH3", "1000"},
                {"SK_NIHILANTH_ZAP1", "30"},
                {"SK_NIHILANTH_ZAP2", "30"},
                {"SK_NIHILANTH_ZAP3", "50"},
                {"SK_PLAYER_ARM1", "1"},
                {"SK_PLAYER_ARM2", "1"},
                {"SK_PLAYER_ARM3", "1"},
                {"SK_PLAYER_CHEST1", "1"},
                {"SK_PLAYER_CHEST2", "1"},
                {"SK_PLAYER_CHEST3", "1"},
                {"SK_PLAYER_HEAD1", "3"},
                {"SK_PLAYER_HEAD2", "3"},
                {"SK_PLAYER_HEAD3", "3"},
                {"SK_PLAYER_LEG1", "1"},
                {"SK_PLAYER_LEG2", "1"},
                {"SK_PLAYER_LEG3", "1"},
                {"SK_PLAYER_STOMACH1", "1"},
                {"SK_PLAYER_STOMACH2", "1"},
                {"SK_PLAYER_STOMACH3", "1"},
                {"SK_PLR_357_BULLET1", "40"},
                {"SK_PLR_357_BULLET2", "40"},
                {"SK_PLR_357_BULLET3", "40"},
                {"SK_PLR_9MM_BULLET1", "8"},
                {"SK_PLR_9MM_BULLET2", "8"},
                {"SK_PLR_9MM_BULLET3", "8"},
                {"SK_PLR_9MMAR_BULLET1", "5"},
                {"SK_PLR_9MMAR_BULLET2", "5"},
                {"SK_PLR_9MMAR_BULLET3", "5"},
                {"SK_PLR_9MMAR_GRENADE1", "100"},
                {"SK_PLR_9MMAR_GRENADE2", "100"},
                {"SK_PLR_9MMAR_GRENADE3", "100"},
                {"SK_PLR_BUCKSHOT1", "5"},
                {"SK_PLR_BUCKSHOT2", "5"},
                {"SK_PLR_BUCKSHOT3", "5"},
                {"SK_PLR_CROWBAR1", "10"},
                {"SK_PLR_CROWBAR2", "10"},
                {"SK_PLR_CROWBAR3", "10"},
                {"SK_PLR_EGON_NARROW1", "6"},
                {"SK_PLR_EGON_NARROW2", "6"},
                {"SK_PLR_EGON_NARROW3", "6"},
                {"SK_PLR_EGON_WIDE1", "14"},
                {"SK_PLR_EGON_WIDE2", "14"},
                {"SK_PLR_EGON_WIDE3", "14"},
                {"SK_PLR_GAUSS1", "20"},
                {"SK_PLR_GAUSS2", "20"},
                {"SK_PLR_GAUSS3", "20"},
                {"SK_PLR_HAND_GRENADE1", "100"},
                {"SK_PLR_HAND_GRENADE2", "100"},
                {"SK_PLR_HAND_GRENADE3", "100"},
                {"SK_PLR_RPG1", "100"},
                {"SK_PLR_RPG2", "100"},
                {"SK_PLR_RPG3", "100"},
                {"SK_PLR_SATCHEL1", "150"},
                {"SK_PLR_SATCHEL2", "150"},
                {"SK_PLR_SATCHEL3", "150"},
                {"SK_PLR_TRIPMINE1", "150"},
                {"SK_PLR_TRIPMINE2", "150"},
                {"SK_PLR_TRIPMINE3", "150"},
                {"SK_PLR_XBOW_BOLT_CLIENT1", "10"},
                {"SK_PLR_XBOW_BOLT_CLIENT2", "10"},
                {"SK_PLR_XBOW_BOLT_CLIENT3", "10"},
                {"SK_PLR_XBOW_BOLT_MONSTER1", "50"},
                {"SK_PLR_XBOW_BOLT_MONSTER2", "50"},
                {"SK_PLR_XBOW_BOLT_MONSTER3", "50"},
                {"SK_SCIENTIST_HEAL1", "25"},
                {"SK_SCIENTIST_HEAL2", "25"},
                {"SK_SCIENTIST_HEAL3", "25"},
                {"SK_SCIENTIST_HEALTH1", "20"},
                {"SK_SCIENTIST_HEALTH2", "20"},
                {"SK_SCIENTIST_HEALTH3", "20"},
                {"SK_SENTRY_HEALTH1", "40"},
                {"SK_SENTRY_HEALTH2", "40"},
                {"SK_SENTRY_HEALTH3", "50"},
                {"SK_SNARK_DMG_BITE1", "10"},
                {"SK_SNARK_DMG_BITE2", "10"},
                {"SK_SNARK_DMG_BITE3", "10"},
                {"SK_SNARK_DMG_POP1", "5"},
                {"SK_SNARK_DMG_POP2", "5"},
                {"SK_SNARK_DMG_POP3", "5"},
                {"SK_SNARK_HEALTH1", "2"},
                {"SK_SNARK_HEALTH2", "2"},
                {"SK_SNARK_HEALTH3", "2"},
                {"SK_SUITCHARGER1", "75"},
                {"SK_SUITCHARGER2", "50"},
                {"SK_SUITCHARGER3", "35"},
                {"SK_TURRET_HEALTH1", "50"},
                {"SK_TURRET_HEALTH2", "50"},
                {"SK_TURRET_HEALTH3", "60"},
                {"SK_ZOMBIE_DMG_BOTH_SLASH1", "25"},
                {"SK_ZOMBIE_DMG_BOTH_SLASH2", "40"},
                {"SK_ZOMBIE_DMG_BOTH_SLASH3", "40"},
                {"SK_ZOMBIE_DMG_ONE_SLASH1", "10"},
                {"SK_ZOMBIE_DMG_ONE_SLASH2", "20"},
                {"SK_ZOMBIE_DMG_ONE_SLASH3", "20"},
                {"SK_ZOMBIE_HEALTH1", "50"},
                {"SK_ZOMBIE_HEALTH2", "50"},
                {"SK_ZOMBIE_HEALTH3", "100"},
                {"SKILL", "1"},
                {"SND_SHOW", "0"},
                {"SV_AIRACCELERATE", "10"},
                {"SV_CHEATS", "0"},
                {"SV_FRICTION", "4"},
                {"SV_GRAVITY", "800"},
                {"SV_WATERACCELERATE", "10"},
                {"SV_WATERFRICTION", "1"},
                {"S_SHOW", "0"}
            };
            var demonode = new TreeNode(Path.GetFileName(info.Key)) { ForeColor = Color.LightCoral };
            for (int i = 0; i < info.Value.GsDemoInfo.IncludedBXtData.Count; i++)
            {
                int jp = 0, jm = 0, dp = 0, dm = 0;
                var datanode = new TreeNode("\nBXT Data Frame [" + i + "]") { ForeColor = Color.LightPink };
                for (int index = 0; index < info.Value.GsDemoInfo.IncludedBXtData[i].Objects.Count; index++)
                {
                    KeyValuePair<Bxt.RuntimeDataType, Bxt.BXTData> t = info.Value.GsDemoInfo.IncludedBXtData[i].Objects[index];
                    switch (t.Key)
                    {
                        case Bxt.RuntimeDataType.VERSION_INFO:
                            {
                                ret +=("\t" + "BXT Version: " + ((((Bxt.VersionInfo)t.Value).bxt_version == bxtVersion) ? "Latest" : ("INVALID=" + ((Bxt.VersionInfo)t.Value).bxt_version)) + "\n");
                                ret +=("\t" + "Game Version: " + ((Bxt.VersionInfo)t.Value).build_number + "\n");
                                datanode.Nodes.Add(new TreeNode("Version info")
                                {
                                    ForeColor = Color.PaleVioletRed,
                                    Nodes =
                                    {
                                        new TreeNode("Game version: " + ((Bxt.VersionInfo) t.Value).build_number) { ForeColor = Color.PaleVioletRed },
                                        new TreeNode("BXT Version: " + ((Bxt.VersionInfo) t.Value).bxt_version) { ForeColor = Color.PaleVioletRed }
                                    },
                                });
                                break;
                            }
                        case Bxt.RuntimeDataType.CVAR_VALUES:
                            {
                                foreach (var cvar in ((Bxt.CVarValues)t.Value).CVars.Where(cvar => cvarRules.ContainsKey(cvar.Key.ToUpper())).Where(cvar => cvarRules[cvar.Key.ToUpper()] != cvar.Value.ToUpper()))
                                {
                                    ret +=("\t" + "Illegal Cvar: " + cvar.Key + " " + cvar.Value + "\n");
                                }
                                var cvarnode = new TreeNode("Cvars [" + ((Bxt.CVarValues)t.Value).CVars.Count + "]")
                                {
                                    ForeColor = Color.LightBlue
                                };
                                cvarnode.Nodes.AddRange(
                                    ((Bxt.CVarValues)t.Value).CVars.Select(
                                        x => new TreeNode(x.Key + " " + x.Value) { ForeColor = Color.LightBlue }).ToArray());
                                datanode.Nodes.Add(cvarnode);
                                break;
                            }
                        case Bxt.RuntimeDataType.TIME:
                            {
                                if (i+1 == info.Value.GsDemoInfo.IncludedBXtData.Count)
                                {
                                    ret +=("\t" + "Demo bxt time: " + ((Bxt.Time)t.Value).ToString() + " Frame: " + i + "\n");
                                }
                                datanode.Nodes.Add(new TreeNode("Time: " + ((Bxt.Time)t.Value).ToString())
                                {
                                    ForeColor = Color.Yellow
                                });
                                break;
                            }
                        case Bxt.RuntimeDataType.BOUND_COMMAND:
                            {
                                if (((Bxt.BoundCommand)t.Value).command.ToUpper().Contains("+JUMP"))
                                    jp++;
                                if (((Bxt.BoundCommand)t.Value).command.ToUpper().Contains("-JUMP"))
                                    jm++;
                                if (((Bxt.BoundCommand)t.Value).command.ToUpper().Contains("+DUCK"))
                                    dp++;
                                if (((Bxt.BoundCommand)t.Value).command.ToUpper().Contains("-DUCK"))
                                    dm++;
                                if (((Bxt.BoundCommand) t.Value).command.ToUpper().Contains(";"))
                                {
                                    ret +=("\t" + "Possible script: " + ((Bxt.BoundCommand)t.Value).command + " Frame: " + i + "\n");
                                }
                                datanode.Nodes.Add(new TreeNode("Bound command: " + ((Bxt.BoundCommand)t.Value).command)
                                {
                                    ForeColor = Color.LightSalmon
                                });
                                break;
                            }
                        case Bxt.RuntimeDataType.ALIAS_EXPANSION:
                            {
                                ret +=("\t" + "Alias [" + ((Bxt.AliasExpansion)t.Value).name + "]: " + ((Bxt.AliasExpansion)t.Value).command + " Frame: " + i + "\n");
                                datanode.Nodes.Add(new TreeNode("Alias [" + ((Bxt.AliasExpansion)t.Value).name + "]: " + ((Bxt.AliasExpansion)t.Value).command) { ForeColor = Color.LightCyan });
                                break;
                            }
                        case Bxt.RuntimeDataType.SCRIPT_EXECUTION:
                            {
                                ret +=("\t" + "Config execution: " + ((Bxt.ScriptExecution)t.Value).filename + " Frame: " + i + "\n");
                                //File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\extracted\\" + Path.GetRandomFileName() + ".cfg", ((Bxt.ScriptExecution)t.Value).contents);
                                datanode.Nodes.Add(new TreeNode("Script: " + ((Bxt.ScriptExecution)t.Value).filename)
                                {
                                    ForeColor = Color.LightSteelBlue,
                                    Nodes =
                                    {
                                        new TreeNode(((Bxt.ScriptExecution) t.Value).contents) {ForeColor = Color.LightSteelBlue}
                                    }
                                });
                                break;
                            }
                        case Bxt.RuntimeDataType.COMMAND_EXECUTION:
                            {
                                if (((Bxt.CommandExecution) t.Value).command.ToUpper().Contains("+JUMP"))
                                {
                                    if (jp == 0)
                                        ret += ("\t" + "Possible autojump: " + ((Bxt.CommandExecution)t.Value).command + " Frame: " + i + "\n");
                                    else
                                        jp--;
                                }
                                if (((Bxt.CommandExecution) t.Value).command.ToUpper().Contains("-JUMP"))
                                {
                                    if (jm == 0)
                                        ret += ("\t" + "Possible autojump: " + ((Bxt.CommandExecution)t.Value).command + " Frame: " + i + "\n");
                                    else
                                        jm--;
                                }
                                if (((Bxt.CommandExecution) t.Value).command.ToUpper().Contains("+DUCK"))
                                {
                                    if (dp == 0)
                                        ret += ("\t" + "Possible ducktap: " + ((Bxt.CommandExecution)t.Value).command + " Frame: " + i + "\n");
                                    else
                                        dp--;
                                }
                                if (((Bxt.CommandExecution)t.Value).command.ToUpper().Contains("-DUCK"))
                                {
                                    if (dm == 0)
                                        ret += ("\t" + "Possible ducktap: " + ((Bxt.CommandExecution)t.Value).command + " Frame: " + i + "\n");
                                    else
                                        dm--;
                                }
                                if (((Bxt.CommandExecution)t.Value).command.ToUpper().ToUpper().Contains("BXT"))
                                {
                                    ret +=("\t" + "Disallowed bxt command: " + ((Bxt.CommandExecution)t.Value).command + " Frame: " + i + "\n");
                                }
                                datanode.Nodes.Add(new TreeNode("Command: " + ((Bxt.CommandExecution)t.Value).command)
                                {
                                    ForeColor = Color.LightGreen
                                });
                                if (((Bxt.CommandExecution)t.Value).command.ToUpper().ToUpper().Contains("HOST_"))
                                {
                                    ret +=("\t" + "Disallowed host_ command: " + ((Bxt.CommandExecution)t.Value).command + " Frame: " + i + "\n");
                                }
                                if (((Bxt.CommandExecution)t.Value).command.ToUpper().ToUpper().Contains("SV_"))
                                {
                                    ret +=("\t" + "Disallowed sv_ command: " + ((Bxt.CommandExecution)t.Value).command + " Frame: " + i + "\n");
                                }
                                if (((Bxt.CommandExecution)t.Value).command.ToUpper().ToUpper().Contains("SK_"))
                                {
                                    ret +=("\t" + "Disallowed sk_ command: " + ((Bxt.CommandExecution)t.Value).command + " Frame: " + i + "\n");
                                }
								if (((Bxt.CommandExecution)t.Value).command.ToUpper().ToUpper().Contains("SKILL"))
								{
								    ret +=("\t" + "Disallowed skill command: " + ((Bxt.CommandExecution)t.Value).command + " Frame: " + i + "\n");
								}
                                if (((Bxt.CommandExecution)t.Value).command.ToUpper().ToUpper().StartsWith("LOAD"))
                                {
                                    ret +=("\t" + ((Bxt.CommandExecution)t.Value).command + "\n");
                                }
                                if (((Bxt.CommandExecution)t.Value).command.ToUpper().ToUpper().Contains("WAIT"))
                                {
                                    ret +=("\t" + "Disallowed wait command: " + ((Bxt.CommandExecution)t.Value).command + " Frame: " + i + "\n");
                                }
                                if (((Bxt.CommandExecution)t.Value).command.ToUpper().ToUpper().Contains("CONNECT"))
                                {
                                    ret +=("\t" + "Disallowed connect command: " + ((Bxt.CommandExecution)t.Value).command + " Frame: " + i + "\n");
                                }
                                break;
                            }
                            
                        case Bxt.RuntimeDataType.GAME_END_MARKER:
                            {
                                datanode.Nodes.Add(new TreeNode("-- GAME END --") { ForeColor = Color.ForestGreen });
                                break;
                            }
                        case Bxt.RuntimeDataType.LOADED_MODULES:
                            {
                                var modulesnode = new TreeNode("Loaded modules [" + ((Bxt.LoadedModules)t.Value).filenames.Count + "]") { ForeColor = Color.LightGreen };
                                modulesnode.Nodes.AddRange(((Bxt.LoadedModules)t.Value).filenames.Select(x => new TreeNode(x) { ForeColor = Color.LightGreen }).ToArray());
                                datanode.Nodes.Add(modulesnode);
                                break;
                            }
                        case Bxt.RuntimeDataType.CUSTOM_TRIGGER_COMMAND:
                            {
                                var trigger = (Bxt.CustomTriggerCommand)t.Value;
                                ret +=("\t" + $"Custom trigger X1:{trigger.corner_max.X} Y1:{trigger.corner_max.Y} Z1:{trigger.corner_max.Z} X2:{trigger.corner_min.X} Y2:{trigger.corner_min.Y} Z2:{trigger.corner_min.Z}" + " Frame: " + i + "\n");
                                datanode.Nodes.Add(new TreeNode($"Custom trigger X1:{trigger.corner_max.X} Y1:{trigger.corner_max.Y} Z1:{trigger.corner_max.Z} X2:{trigger.corner_min.X} Y2:{trigger.corner_min.Y} Z2:{trigger.corner_min.Z}")
                                {
                                    ForeColor = Color.Orange,
                                    Nodes = { new TreeNode("Command: " + trigger.command) { ForeColor = Color.Orange } }
                                });
                                break;
                            }
                        default:
                            {
                                datanode.Nodes.Add(new TreeNode("Invalid bxt data!") { ForeColor = Color.Red });
                                break;
                            }
                    }
                }
                demonode.Nodes.Add(datanode);
            }
            BXTTreeView.Nodes.Add(demonode);
            ret += "\n";
            return ret;
        }

        private void Verification_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void Verification_DragDrop(object sender, DragEventArgs e)
        {
            var dropfiles = (string[]) e.Data.GetData(DataFormats.FileDrop);
            Verify(dropfiles);
            e.Effect = DragDropEffects.None;
        }
    }
}
