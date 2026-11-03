using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OsuLazerFix
{
    /// <summary>
    /// Small helpers around WPF animations. When animations are off (Settings - Animations) every change is applied at once,
    /// and the endless effects (pulsing dot, shimmer, rotating ring) stop.
    /// </summary>
    internal static class Anim
    {
        /// <summary>The user setting.</summary>
        public static bool On = true;

        /// <summary>On and the window is not minimised: endless effects run only then.</summary>
        public static bool Lively { get; private set; }

        public static readonly IEasingFunction Out = Freeze(new CubicEase { EasingMode = EasingMode.EaseOut });
        public static readonly IEasingFunction InOut = Freeze(new SineEase { EasingMode = EasingMode.EaseInOut });
        public static readonly IEasingFunction Soft = Freeze(new QuinticEase { EasingMode = EasingMode.EaseOut });
        public static readonly IEasingFunction Pop = Freeze(new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.45 });
        public static readonly IEasingFunction In = Freeze(new CubicEase { EasingMode = EasingMode.EaseIn });

        private static IEasingFunction Freeze(EasingFunctionBase e)
        {
            e.Freeze();
            return e;
        }

        // the newest animation of each property, so a finished one only cleans up if nothing replaced it
        private static readonly ConditionalWeakTable<DependencyObject, Dictionary<DependencyProperty, AnimationTimeline>> latest =
            new ConditionalWeakTable<DependencyObject, Dictionary<DependencyProperty, AnimationTimeline>>();

        private static void Run(IAnimatable target, DependencyProperty prop, DoubleAnimation a, double to, Action done)
        {
            var obj = (DependencyObject)target;
            Dictionary<DependencyProperty, AnimationTimeline> map = latest.GetOrCreateValue(obj);
            map[prop] = a;
            a.Completed += delegate
            {
                AnimationTimeline cur;
                if (map.TryGetValue(prop, out cur) && cur == a)
                {
                    // keep the end value as the plain value and drop the animation, so later direct changes work
                    map.Remove(prop);
                    target.BeginAnimation(prop, null);
                    obj.SetValue(prop, to);
                }
                if (done != null) done();
            };
            target.BeginAnimation(prop, a, HandoffBehavior.SnapshotAndReplace);
        }

        private static void Instant(IAnimatable target, DependencyProperty prop, double to, Action done)
        {
            var obj = (DependencyObject)target;
            Dictionary<DependencyProperty, AnimationTimeline> map;
            if (latest.TryGetValue(obj, out map)) map.Remove(prop);
            target.BeginAnimation(prop, null);
            obj.SetValue(prop, to);
            if (done != null) done();
        }

        /// <summary>Animates a number to a value. done runs at the end (at once when animations are off).</summary>
        public static void To(IAnimatable target, DependencyProperty prop, double to, double ms, IEasingFunction ease = null, double delayMs = 0, Action done = null)
        {
            if (!On || ms <= 0)
            {
                Instant(target, prop, to, done);
                return;
            }

            var a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease ?? Out };
            if (delayMs > 0) a.BeginTime = TimeSpan.FromMilliseconds(delayMs);
            Run(target, prop, a, to, done);
        }

        /// <summary>Animates a number from one value to another.</summary>
        public static void FromTo(IAnimatable target, DependencyProperty prop, double from, double to, double ms, IEasingFunction ease = null, double delayMs = 0, Action done = null)
        {
            if (!On || ms <= 0)
            {
                Instant(target, prop, to, done);
                return;
            }

            var a = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease ?? Out };
            if (delayMs > 0)
            {
                a.BeginTime = TimeSpan.FromMilliseconds(delayMs);
                target.BeginAnimation(prop, null);
                ((DependencyObject)target).SetValue(prop, from);   // hold the start value while waiting
            }
            Run(target, prop, a, to, done);
        }

        /// <summary>Animates the colour of a (not frozen) brush.</summary>
        public static void Color(SolidColorBrush brush, Color to, double ms)
        {
            if (brush == null || brush.IsFrozen) return;
            if (!On || ms <= 0)
            {
                brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
                brush.Color = to;
                return;
            }
            var a = new ColorAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = Out };
            brush.BeginAnimation(SolidColorBrush.ColorProperty, a, HandoffBehavior.SnapshotAndReplace);
        }

        /// <summary>Animates the colour of a gradient stop.</summary>
        public static void Stop(GradientStop stop, Color to, double ms)
        {
            if (!On || ms <= 0)
            {
                stop.BeginAnimation(GradientStop.ColorProperty, null);
                stop.Color = to;
                return;
            }
            stop.BeginAnimation(GradientStop.ColorProperty, new ColorAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = Out }, HandoffBehavior.SnapshotAndReplace);
        }

        // ------------------------------------------------------------------ endless effects

        private sealed class Loop
        {
            public IAnimatable Target;
            public DependencyProperty Prop;
            public Func<AnimationTimeline> Make;
            public double Rest;
            public bool Wanted = true;
            public bool Running;
        }

        private static readonly List<Loop> loops = new List<Loop>();

        /// <summary>Registers an endless animation. It runs only while animations are on and the window is visible.</summary>
        public static object Forever(IAnimatable target, DependencyProperty prop, Func<AnimationTimeline> make, double rest, bool wanted = true)
        {
            var l = new Loop { Target = target, Prop = prop, Make = make, Rest = rest, Wanted = wanted };
            loops.Add(l);
            Apply(l);
            return l;
        }

        /// <summary>Starts or stops one endless animation (for example the pulse only while the fix is installed).</summary>
        public static void Want(object handle, bool wanted)
        {
            var l = handle as Loop;
            if (l == null || l.Wanted == wanted) return;
            l.Wanted = wanted;
            Apply(l);
        }

        public static void SetLively(bool lively)
        {
            Lively = lively;
            foreach (Loop l in loops) Apply(l);
        }

        private static void Apply(Loop l)
        {
            bool run = Lively && l.Wanted;
            if (run == l.Running) return;
            l.Running = run;
            if (run)
            {
                l.Target.BeginAnimation(l.Prop, l.Make());
            }
            else
            {
                l.Target.BeginAnimation(l.Prop, null);
                ((DependencyObject)l.Target).SetValue(l.Prop, l.Rest);
            }
        }

        /// <summary>An endless back-and-forth number animation.</summary>
        public static AnimationTimeline Wave(double from, double to, double ms, double delayMs = 0)
        {
            var a = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(ms)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = InOut };
            if (delayMs > 0) a.BeginTime = TimeSpan.FromMilliseconds(delayMs);
            return a;
        }

        /// <summary>An endless one-way number animation (rotation, ripple), with an optional pause before each repeat.</summary>
        public static AnimationTimeline Repeat(double from, double to, double ms, double pauseMs = 0, IEasingFunction ease = null)
        {
            if (pauseMs <= 0)
                return new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(ms)) { RepeatBehavior = RepeatBehavior.Forever, EasingFunction = ease };

            var frames = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(ms + pauseMs), RepeatBehavior = RepeatBehavior.Forever };
            frames.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            frames.KeyFrames.Add(new EasingDoubleKeyFrame(to, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms)), ease));
            frames.KeyFrames.Add(new DiscreteDoubleKeyFrame(to, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ms + pauseMs))));
            return frames;
        }
    }
}
