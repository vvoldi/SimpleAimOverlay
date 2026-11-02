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
            using(UiMotion.Transition(host)){host.BackColor=System.Drawing.Color.Navy;}
            Settle();if(host.Controls.Cast<Control>().Any(c=>c.GetType().Name=="FadeLayer"))throw new Exception("Transition layer must be removed after completion");host.Close();
        }
        using(var content=new Panel{AutoScroll=true})using(var host=new Form()){
            content.Controls.Add(new Panel{Height=1500,Dock=DockStyle.Top});var scroll=new StyledScrollHost(content){Dock=DockStyle.Fill};host.Controls.Add(scroll);host.Show();Settle();
            content.AutoScrollPosition=new System.Drawing.Point(0,200);Settle();
            if(content.AutoScrollPosition.Y>=0)throw new Exception("Themed scrollbar host must preserve scrolling");
            scroll.SmoothWheel(-120);scroll.SmoothWheel(-120);Settle();
            if(content.AutoScrollPosition.Y!=-296)throw new Exception("Smooth wheel must accumulate consecutive input and settle exactly");
            scroll.SmoothWheel(120);Settle();
            if(content.AutoScrollPosition.Y!=-248)throw new Exception("Smooth wheel must reverse without jumping");host.Close();
        }
        Console.WriteLine("PASS: dialogs fade to full opacity, slider values settle exactly, transition layers dispose and themed scrolling works.");
    }
}
