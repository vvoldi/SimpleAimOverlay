using SimpleAimOverlay;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;

internal static class MotionChecks
{
    internal static void Run()
    {
        static void Settle(){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<350){Application.DoEvents();Thread.Sleep(5);}}
        using(var form=new StyledDialog()) {
            if(form.Opacity!=0)throw new Exception("Dialog must start hidden until layout is ready");
            form.Show();Settle();
            if(form.Opacity!=1)throw new Exception("Dialog fade must finish fully opaque");
            form.Close();
        }
        using(var host=new Form()){var slider=new DarkSlider(10,240){Dock=DockStyle.Top};host.Controls.Add(slider);host.Show();
            slider.Value=173;Settle();
            if(slider.Value!=173||Math.Abs(slider.VisualValue-173)>.05)throw new Exception("Slider animation must settle at the exact selected value");
            var page=new Panel{Bounds=new System.Drawing.Rectangle(0,50,200,100)};host.Controls.Add(page);
            using(UiMotion.Transition(page)){if(UiMotion.Enabled && !host.Controls.Cast<Control>().Any(c=>c.GetType().Name=="FadeLayer"))throw new Exception("Old frame must cover the page before mutation");page.BackColor=System.Drawing.Color.Navy;}
            Settle();if(host.Controls.Cast<Control>().Any(c=>c.GetType().Name=="FadeLayer"))throw new Exception("Transition layer must be removed after completion");host.Close();
        }
        using(var content=new Panel{AutoScroll=true})using(var host=new Form()){
            content.Controls.Add(new Panel{Height=1500,Dock=DockStyle.Top});var scroll=new StyledScrollHost(content){Dock=DockStyle.Fill};host.Controls.Add(scroll);host.Show();Settle();
            content.AutoScrollPosition=new System.Drawing.Point(0,200);Settle();
            if(content.AutoScrollPosition.Y>=0)throw new Exception("Themed scrollbar host must preserve scrolling");
            scroll.SmoothWheel(-120);scroll.SmoothWheel(-120);Settle();
            if(content.AutoScrollPosition.Y!=-296)throw new Exception("Smooth wheel must accumulate consecutive input and settle exactly");
            scroll.SmoothWheel(120);Settle();
            if(content.AutoScrollPosition.Y!=-248)throw new Exception("Smooth wheel must reverse without jumping");
            content.AutoScrollPosition=new System.Drawing.Point(0,200);
            scroll.SmoothWheel(-480);scroll.SmoothWheel(120);Settle();
            if(content.AutoScrollPosition.Y!=-152)throw new Exception("Reversing mid-animation must discard pending downward movement");
            scroll.SmoothWheel(2400);Settle();
            if(content.AutoScrollPosition.Y!=0)throw new Exception("Upward scrolling must stop at the top");
            scroll.SmoothWheel(120);scroll.SmoothWheel(-120);Settle();
            if(content.AutoScrollPosition.Y!=-48)throw new Exception("Scrolling away from the top must respond immediately");
            scroll.SmoothWheel(-480);content.AutoScrollPosition=new System.Drawing.Point(0,100);Settle();
            if(content.AutoScrollPosition.Y!=-100)throw new Exception("External scroll must cancel stale animation instead of snapping back");host.Close();
        }
        using(var host=new Form {ClientSize=new System.Drawing.Size(900,580)}) {
            var feedback=new FeedbackPanel(()=>new StoreData());
            host.Controls.Add(new StyledScrollHost(feedback){Dock=DockStyle.Fill});host.Show();Settle();
            if(feedback.VerticalScroll.Visible)throw new Exception("Feedback must fit without page scrolling at normal size");
            host.ClientSize=new System.Drawing.Size(500,260);Settle();
            if(!feedback.VerticalScroll.Visible)throw new Exception("Feedback must remain scrollable when the form does not fit");
            host.Close();
        }
        Console.WriteLine("PASS: dialogs fade to full opacity, slider values settle exactly, transition layers dispose and themed scrolling works.");
        using(var host=new Form {ClientSize=new System.Drawing.Size(400,300)}) {
            var list=new DarkListBox {Dock=DockStyle.Fill,BackColor=DarkTheme.Card,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=40};
            list.DrawItem+=(_,e)=>e.Graphics.Clear(DarkTheme.Card);
            for(int i=0;i<30;i++)list.Items.Add("Drawing "+i);
            host.Controls.Add(list);host.Show();Settle();
            using var frame=new System.Drawing.Bitmap(400,300);list.DrawToBitmap(frame,list.ClientRectangle);
            for(int y=5;y<295;y++)for(int x=370;x<400;x++) {
                var pixel=frame.GetPixel(x,y);
                if(pixel.R>180 && pixel.G>180 && pixel.B>180)throw new Exception("Editor transition snapshot must not contain the clipped native white scrollbar");
            }
            host.Close();
        }
    }
}
