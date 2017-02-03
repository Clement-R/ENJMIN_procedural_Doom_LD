using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Geometry;
using CodeImp.DoomBuilder.Map;
using CodeImp.DoomBuilder.Config;
using System.Threading;

namespace GenerativeDoom
{
    public partial class GDForm : Form
    {

        public const int TYPE_PLAYER_START = 1;

        private IList<DrawnVertex> points;
        private bool startPointPlaced = false;

        public GDForm()
        {
            InitializeComponent();
            points = new List<DrawnVertex>();
        }

        // We're going to use this to show the form
        public void ShowWindow(Form owner)
        {
            // Position this window in the left-top corner of owner
            this.Location = new Point(owner.Location.X + 20, owner.Location.Y + 90);

            // Show it
            base.Show(owner);
        }

        // Form is closing event
        private void GDForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // When the user is closing the window we want to cancel this, because it
            // would also unload (dispose) the form. We only want to hide the window
            // so that it can be re-used next time when this editing mode is activated.
            if (e.CloseReason == CloseReason.UserClosing)
            {
                // Just cancel the editing mode. This will automatically call
                // OnCancel() which will switch to the previous mode and in turn
                // calls OnDisengage() which hides this window.
                General.Editing.CancelMode();
                e.Cancel = true;
            }
        }

        private void GDForm_Load(object sender, EventArgs e)
        {

        }

        private void BtnClose_Click(object sender, EventArgs e)
        {
            General.Editing.CancelMode();
        }

        private void newSector(IList<DrawnVertex> points, int lumi, int ceil, int floor)
        {
            Console.Write("New sector: ");
            List<DrawnVertex> pSector = new List<DrawnVertex>();

            DrawnVertex v;
            v = new DrawnVertex();
            foreach (DrawnVertex p in points)
            {
                Console.Write(" p:"+p.pos);
                v.pos = p.pos;
                v.stitch = true;
                v.stitchline = true;
                pSector.Add(v);
            }

            Console.Write("\n");

            v.pos = points[0].pos;
            v.stitch = true;
            v.stitchline = true;

            pSector.Add(v);

            Tools.DrawLines(pSector);

            // Snap to map format accuracy
            General.Map.Map.SnapAllToAccuracy();

            // Clear selection
            General.Map.Map.ClearAllSelected();

            // Update cached values
            General.Map.Map.Update();

            // Edit new sectors?
            List<Sector> newsectors = General.Map.Map.GetMarkedSectors(true);

            foreach(Sector s in newsectors)
            {
                s.CeilHeight = ceil;
                s.FloorHeight = floor;
                s.Brightness = lumi;
            }


            // Update the used textures
            General.Map.Data.UpdateUsedTextures();

            // Map is changed
            General.Map.IsChanged = true;

            General.Interface.RedrawDisplay();

            //Ajoute, on enleve la marque sur les nouveaux secteurs
            General.Map.Map.ClearMarkedSectors(false);
        }

        private void newSector(DrawnVertex top_left, float width, float height, int lumi, int ceil, int floor)
        {
            points.Clear();
            DrawnVertex v;
            v = new DrawnVertex();
            v.pos = top_left.pos;
            points.Add(v);

            v.pos.y += height;
            points.Add(v);

            v.pos.x += width;
            
            points.Add(v);

            v.pos.y -= height;
            points.Add(v);

            newSector(points,lumi,ceil,floor);

            points.Clear();
        }

        private Thing addThing(Vector2D pos, String category, float proba = 0.5f)
        {
            Thing t = addThing(pos);
            if (t != null)
            {

                IList<ThingCategory> cats = General.Map.Data.ThingCategories;
                Random r = new Random();

                bool found = false;
                foreach (ThingTypeInfo ti in General.Map.Data.ThingTypes)
                {
                    if (ti.Category.Name == category)
                    {
                        t.Type = ti.Index;
                        Console.WriteLine("Add thing cat " + category + " for thing at pos " + pos);
                        found = true;
                        if (r.NextDouble() > proba)                     
                            break;
                    }

                }
                if (!found)
                {
                    Console.WriteLine("###### Could not find category " + category + " for thing at pos " + pos);
                }else
                    t.Rotate(0);
            }else
            {
                Console.WriteLine("###### Could not add thing for cat " + category + " at pos " + pos);
            }

            return t;
        }

        private Thing addThing(Vector2D pos)
        {
            if (pos.x < General.Map.Config.LeftBoundary || pos.x > General.Map.Config.RightBoundary ||
                pos.y > General.Map.Config.TopBoundary || pos.y < General.Map.Config.BottomBoundary)
            {
                Console.WriteLine( "Error Generaetive Doom: Failed to insert thing: outside of map boundaries.");
                return null;
            }

            // Create thing
            Thing t = General.Map.Map.CreateThing();
            if (t != null)
            {
                General.Settings.ApplyDefaultThingSettings(t);

                t.Move(pos);

                t.UpdateConfiguration();

                // Update things filter so that it includes this thing
                General.Map.ThingsFilter.Update();

                // Snap to map format accuracy
                t.SnapToAccuracy();
            }

            return t;
        }

        private void correctMissingTex()
        {

            String defaulttexture = "-";
            if (General.Map.Data.TextureNames.Count > 1)
                defaulttexture = General.Map.Data.TextureNames[1];

            // Go for all the sidedefs
            foreach (Sidedef sd in General.Map.Map.Sidedefs)
            {
                // Check upper texture. Also make sure not to return a false
                // positive if the sector on the other side has the ceiling
                // set to be sky
                if (sd.HighRequired() && sd.HighTexture[0] == '-')
                {
                    if (sd.Other != null && sd.Other.Sector.CeilTexture != General.Map.Config.SkyFlatName)
                    {
                        sd.SetTextureHigh(General.Settings.DefaultCeilingTexture);
                    }
                }

                // Check middle texture
                if (sd.MiddleRequired() && sd.MiddleTexture[0] == '-')
                {
                    sd.SetTextureMid(General.Settings.DefaultTexture);
                }

                // Check lower texture. Also make sure not to return a false
                // positive if the sector on the other side has the floor
                // set to be sky
                if (sd.LowRequired() && sd.LowTexture[0] == '-')
                {
                    if (sd.Other != null && sd.Other.Sector.FloorTexture != General.Map.Config.SkyFlatName)
                    {
                        sd.SetTextureLow(General.Settings.DefaultFloorTexture);
                    }
                }

            }
        }

        private bool checkIntersect(Line2D measureline)
        {
            bool inter = false;
            foreach (Linedef ld2 in General.Map.Map.Linedefs)
            {
                // Intersecting?
                // We only keep the unit length from the start of the line and
                // do the real splitting later, when all intersections are known
                float u;
                if (ld2.Line.GetIntersection(measureline, out u))
                {
                    if (!float.IsNaN(u) && (u > 0.0f) && (u < 1.0f))
                    {
                        inter = true;
                        break;
                    }
                       
                }
            }

            Console.WriteLine("Chevk inter " + measureline + " is " + inter);

            return inter;
        }

        private bool checkIntersect(Line2D measureline, out float closestIntersect)
        {
            // Check if any other lines intersect this line
            List<float> intersections = new List<float>();
            foreach (Linedef ld2 in General.Map.Map.Linedefs)
            {
                // Intersecting?
                // We only keep the unit length from the start of the line and
                // do the real splitting later, when all intersections are known
                float u;
                if (ld2.Line.GetIntersection(measureline, out u))
                {
                   if (!float.IsNaN(u) && (u > 0.0f) && (u < 1.0f))
                        intersections.Add(u);
                }
            }

            if(intersections.Count() > 0)
            {
                // Sort the intersections
                intersections.Sort();

                closestIntersect = intersections.First<float>();

                return true;
            }

            closestIntersect = 0.0f;
            return false;

            
        }
    

        private void showCategories()
        {
            lbCategories.Items.Clear();
            IList<ThingCategory> cats = General.Map.Data.ThingCategories;
            foreach(ThingCategory cat in cats)
            {
                if (!lbCategories.Items.Contains(cat.Name))
                    lbCategories.Items.Add(cat.Name);
            }
            
        }

        private void makeOnePath( bool playerStart)
        {
            Random r = new Random();

            /*
            DrawnVertex v = new DrawnVertex();
            int lumi = 200;
            int ceil = 128;
            int floor = 0;
            
            float width = (float)(r.Next() % 10) * 64.0f + 128.0f;
            float height = (float)(r.Next() % 10) * 64.0f + 128.0f;

            newSector(v, width, height, lumi, ceil, floor);

            if (playerStart) {
                Thing t = addThing(new Vector2D(v.pos.x + width / 2, v.pos.y + height / 2));
                t.Type = TYPE_PLAYER_START;
                t.Rotate(0);
            }

            width = width / 2;
            v.pos.x = width - 40;
            v.pos.y = height;
            newSector(v, 80, 60, lumi, ceil, floor);

            width = width * 2;
            v.pos.x = 0;
            v.pos.y = height + 60;
            newSector(v, 256, 256, lumi, ceil, floor);
            */

            // -----------------------------------------------------------------------------------------------------------------------
            DrawnVertex v = new DrawnVertex();
            float pwidth = 0.0f;
            float pheight = 0.0f;

            float doorHeight = 60;
            float doorWidth = 80;

            int lumi = 200;
            int ceil = (int)(r.NextDouble() * 128 + 128);
            int floor = (int)(r.NextDouble() * 128 + 128);
            int pfloor = floor;
            int pceil = ceil;
            Vector2D pv = new Vector2D();
            
            int pdir = 0;
            for (int i = 0; i < 5; i++) {

                // TODO
                // First sector ? Create it
                // Else get previous door direction and create a sector linked to last door
                // Choose a direction to go
                // Create a door
                // Stock last chosen direction

                // Game worflow :
                // Player presse use to open a door, it opens a room with another door.
                // Using the second door close the first one and start the room
                // Killing the enemies let the player use the other doors

                // Get previous sector position
                pv = v.pos;

                // Choose height and width of next sector
                // float width = (float)(r.Next() % 10) * 64.0f + 128.0f;
                float width = 256.0f;
                // float height = (float)(r.Next() % 10) * 64.0f + 128.0f;
                float height = 256.0f;

                if (i == 0) {
                    // Set properties of the sector
                    lumi = Math.Min(256, Math.Max(0, lumi + (r.NextDouble() > 0.5 ? 1 : -1) * 16));

                    floor = Math.Min(256, Math.Max(0, pfloor + (r.NextDouble() > 0.5 ? 1 : -1) * 16));
                    ceil = Math.Min(256, Math.Max(0, pceil + (r.NextDouble() > 0.5 ? 1 : -1) * 16));

                    if (ceil - floor < 100) {
                        floor = pfloor;
                        ceil = floor + 180 + (r.NextDouble() > 0.5 ? 1 : -1) * 16;
                    }

                    newSector(v, width, height, lumi, ceil, floor);
                    
                    // Add a door
                    float doorX = (width / 2) * (width > 0 ? 1 : -1);
                    v.pos.x = (width > 0) ? doorX - (doorWidth / 2) : doorX + (doorWidth / 2);
                    v.pos.y = height;

                    newSector(v, doorWidth, doorHeight, lumi, ceil, floor);
                } else {
                    // Set properties of the sector
                    lumi = Math.Min(256, Math.Max(0, lumi + (r.NextDouble() > 0.5 ? 1 : -1) * 16));

                    floor = Math.Min(256, Math.Max(0, pfloor + (r.NextDouble() > 0.5 ? 1 : -1) * 16));
                    ceil = Math.Min(256, Math.Max(0, pceil + (r.NextDouble() > 0.5 ? 1 : -1) * 16));

                    if (ceil - floor < 100) {
                        floor = pfloor;
                        ceil = floor + 180 + (r.NextDouble() > 0.5 ? 1 : -1) * 16;
                    }

                    v.pos.x = pv.x - (pwidth / 2 - doorWidth / 2);
                    v.pos.y = pv.y + doorHeight;

                    newSector(v, width, height, lumi, ceil, floor);

                    // Add a door
                    float doorX = (width / 2) * (width > 0 ? 1 : -1);
                    v.pos.x = (width > 0) ? doorX - (doorWidth / 2) : doorX + (doorWidth / 2) + v.pos.x;
                    v.pos.y = height + v.pos.y;

                    newSector(v, doorWidth, doorHeight, lumi, ceil, floor);
                }
                
                /*
                // We check all 4 directions to see where we can put it
                Line2D l1 = new Line2D();
                bool[] dirOk = new bool[4];

                // Right
                l1.v2 = l1.v1 = pv;
                l1.v1.x += pwidth;
                l1.v2.x += width + 256;
                bool droite = !checkIntersect(l1);
                l1.v1.y = l1.v2.y = l1.v1.y + height;
                droite = droite && !checkIntersect(l1);
                dirOk[0] = droite;
                Console.WriteLine("right ok:" + droite);

                // Left
                l1.v2 = l1.v1 = pv;
                l1.v2.x -= width + 256;
                bool gauche = !checkIntersect(l1);
                l1.v1.y = l1.v2.y = l1.v1.y + height;
                gauche = gauche && !checkIntersect(l1);
                dirOk[1] = gauche;
                Console.WriteLine("left ok:" + gauche);

                // Up
                l1.v2 = l1.v1 = pv;
                l1.v1.y = l1.v2.y = l1.v1.y + pheight;
                l1.v2.y += height + 256;
                bool haut = !checkIntersect(l1);
                l1.v1.x = l1.v2.x = l1.v1.x + width;
                haut = haut && !checkIntersect(l1);
                dirOk[2] = haut;
                Console.WriteLine("up ok:" + haut);

                // Down
                l1.v2 = l1.v1 = pv;
                l1.v2.y -= height + 256;
                bool bas = !checkIntersect(l1);
                l1.v1.x = l1.v2.x = l1.v1.x + width;
                bas = bas && !checkIntersect(l1);
                dirOk[3] = bas;
                Console.WriteLine("Down ok:" + bas);

                bool oneDirOk = haut || bas || gauche || droite;

                int nextDir = pdir;
                if (!oneDirOk)
                    Console.WriteLine("No direction available");
                else {
                    int nbTry = 0;
                    while ((!dirOk[nextDir] || pdir == nextDir) && nbTry++ < 100)
                        nextDir = r.Next() % 4;
                }

                switch (nextDir) {
                    case 0:
                        Console.WriteLine("Right !");
                        v.pos.x += pwidth;
                        break;
                    case 1:
                        Console.WriteLine("Left !");
                        v.pos.x -= width;
                        break;
                    case 2:
                        Console.WriteLine("Up !");
                        v.pos.y += pheight;
                        break;
                    case 3:
                        Console.WriteLine("Down !");
                        v.pos.y -= height;
                        break;

                }
                */
                
                /*
                // Add things to the room
                else if (i == 1) {
                    addThing(new Vector2D(v.pos.x + width / 2, v.pos.y + height / 2), "weapons");
                }
                else if (i % 3 == 0) {
                    while (r.NextDouble() > 0.3f)
                        addThing(new Vector2D(v.pos.x + width / 2 + ((float)r.NextDouble() * (width / 2)) - width / 4,
                            v.pos.y + height / 2 + ((float)r.NextDouble() * (height / 2)) - height / 4), "monsters");
                }
                if (i % 5 == 0) {
                    do {

                        addThing(new Vector2D(v.pos.x + width / 2 + ((float)r.NextDouble() * (width / 2)) - width / 4,
                            v.pos.y + height / 2 + ((float)r.NextDouble() * (height / 2)) - height / 4), "ammunition");
                    } while (r.NextDouble() > 0.3f);
                }
                if (i % 20 == 0) {
                    do {
                        addThing(new Vector2D(v.pos.x + width / 2 + ((float)r.NextDouble() * (width / 2)) - width / 4,
                            v.pos.y + height / 2 + ((float)r.NextDouble() * (height / 2)) - height / 4), "health");
                    } while (r.NextDouble() > 0.5f);
                    addThing(new Vector2D(v.pos.x + width / 2, v.pos.y + height / 2), "weapons", 0.3f);
                }

                while (r.NextDouble() > 0.5f)
                    addThing(new Vector2D(v.pos.x + width / 2 + ((float)r.NextDouble() * (width / 2)) - width / 4,
                        v.pos.y + height / 2 + ((float)r.NextDouble() * (height / 2)) - height / 4), "decoration", (float)r.NextDouble());
                */

                pwidth = width;
                pheight = height;
                pceil = ceil;
                pfloor = floor;
                // pdir = nextDir;

                // Handle thread interruption
                try { Thread.Sleep(0); }
                catch (ThreadInterruptedException) { Console.WriteLine(">>>> thread de generation interrompu at sector " + i); break; }
            }
        }

        private void btnDoMagic_Click(object sender, EventArgs e)
        {
            Console.WriteLine("Trying to do some magic !!");

            makeOnePath(true);
            makeOnePath(false);
            makeOnePath(false);

            correctMissingTex();

            Console.WriteLine("Did I ? Magic ?");
        }

        private void btnAnalysis_Click(object sender, EventArgs e)
        {
            //foreach (ThingTypeInfo ti in General.Map.Data.ThingTypes)
            //Console.WriteLine(ti.Category.Name);
            showCategories();

        }

        private void btnGenerate_Click(object sender, EventArgs e)
        {
            if(startPointPlaced) {
                makeOnePath(false);
            } else {
                makeOnePath(true);
                startPointPlaced = true;
            }
            
            correctMissingTex();
        }
    }
}
