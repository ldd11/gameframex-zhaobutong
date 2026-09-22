using System;

namespace Hotfix.Manager
{
    [Serializable]
    public struct DifferenceSpot
    {
        // Coordinates use the top-left of the image, independent of screen resolution.
        public float x, y, radius;
        public bool rectangular;
        public float left, top, width, height;
        public DifferenceSpot(float x, float y, float radius = .055f) : this()
        { this.x = x; this.y = y; this.radius = radius; }
        public DifferenceSpot(float left, float top, float width, float height, float radius = .055f)
            : this(left + width / 2, top + height / 2, radius)
        {
            rectangular = true;
            this.left = left; this.top = top; this.width = width; this.height = height;
        }
    }

    public sealed class DifferenceRound
    {
        public const int Miss = -1, Ignored = -2;
        public const int MaxSpots = 31;
        readonly DifferenceSpot[] spots;
        readonly bool[] found;
        readonly float imageAspect;
        public int Lives { get; private set; } = 3;
        public int Count { get; private set; }
        public int FoundMask { get; private set; }
        public int Total => spots.Length;
        public bool Complete => Count == Total;
        public bool Finished => Complete || Lives == 0;
        public bool IsFound(int index) => found[index];

        public DifferenceRound(DifferenceSpot[] source, float imageAspect = 1, int foundMask = 0, int lives = 3)
        {
            if (source == null || source.Length == 0 || source.Length > MaxSpots) throw new ArgumentException("关卡需包含 1 至 " + MaxSpots + " 个差异点");
            if (!Finite(imageAspect) || imageAspect <= 0) throw new ArgumentException("图片比例不合法");
            if (foundMask < 0 || (long)foundMask >= (1L << source.Length)) throw new ArgumentException("已找到的差异点存档不合法");
            if (lives < 0 || lives > 3) throw new ArgumentException("剩余机会存档不合法");
            this.imageAspect = imageAspect;
            spots = (DifferenceSpot[])source.Clone();
            foreach (var spot in spots)
            {
                if (!Finite(spot.x) || !Finite(spot.y) || !Finite(spot.radius) ||
                    spot.x < 0 || spot.x > 1 || spot.y < 0 || spot.y > 1 || spot.radius <= 0 || spot.radius > .25f)
                    throw new ArgumentException("差异点坐标或半径不合法");
                if (spot.rectangular && (!Finite(spot.left) || !Finite(spot.top) || !Finite(spot.width) || !Finite(spot.height) ||
                    spot.left < 0 || spot.top < 0 || spot.width <= 0 || spot.height <= 0 ||
                    // Pixel rectangles touching an edge can sum just above 1 after float normalization.
                    spot.left + spot.width > 1.000001f || spot.top + spot.height > 1.000001f))
                    throw new ArgumentException("差异点矩形不合法");
            }
            found = new bool[spots.Length];
            FoundMask = foundMask;
            Lives = lives;
            for (var i = 0; i < found.Length; i++)
            {
                found[i] = (foundMask & (1 << i)) != 0;
                if (found[i]) Count++;
            }
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public int Click(float x, float y)
        {
            if (Finished || !Finite(x) || !Finite(y) || x < 0 || x > 1 || y < 0 || y > 1)
                return Ignored;
            var best = -1;
            var distance = float.MaxValue;
            for (var i = 0; i < spots.Length; i++)
            {
                var dx = x - spots[i].x;
                var dy = (y - spots[i].y) / imageAspect;
                var d = dx * dx + dy * dy;
                var hit = spots[i].rectangular
                    ? x >= spots[i].left && x <= spots[i].left + spots[i].width &&
                      y >= spots[i].top && y <= spots[i].top + spots[i].height
                    : d <= spots[i].radius * spots[i].radius;
                if (hit && d < distance)
                { best = i; distance = d; }
            }
            if (best >= 0) return found[best] ? Ignored : Find(best);
            Lives--;
            return Miss;
        }

        public int Hint()
        {
            if (Finished) return Ignored;
            for (var i = 0; i < found.Length; i++)
                if (!found[i]) return i;
            return Ignored;
        }

        public bool Revive()
        {
            if (Lives != 0 || Complete) return false;
            Lives = 3;
            return true;
        }

        int Find(int index)
        {
            found[index] = true;
            FoundMask |= 1 << index;
            Count++;
            return index;
        }

        // One runnable check, also used by the editor verification menu.
        public static void SelfCheck()
        {
            var points = new[] { new DifferenceSpot(.2f, .3f), new DifferenceSpot(.7f, .8f) };
            var round = new DifferenceRound(points);
            Require(round.Click(.2f, .3f) == 0 && round.Count == 1, "命中");
            Require(round.Click(.2f, .3f) == Ignored && round.Lives == 3, "重复点击不扣血");
            Require(round.Click(float.NaN, .2f) == Ignored && round.Click(-1, 0) == Ignored, "越界输入");
            Require(round.Click(.95f, .05f) == Miss && round.Lives == 2, "误点扣血");
            Require(round.Hint() == 1 && round.Hint() == 1 && round.Count == 1 && round.FoundMask == 1 &&
                    !round.IsFound(1) && round.Lives == 2 && !round.Finished, "重复提示仅查询目标，不改变进度或机会");
            Require(round.Click(.7f, .8f) == 1 && round.Complete && round.FoundMask == 3, "提示后实际点击才通关");
            Require(round.Hint() == Ignored && round.Click(0, 0) == Ignored, "终局不可变");
            round = new DifferenceRound(points);
            for (var i = 0; i < 3; i++) round.Click(0, 0);
            Require(round.Lives == 0 && round.Finished && !round.Complete && round.Hint() == Ignored, "失败");
            var rejected = false;
            try { new DifferenceRound(new[] { new DifferenceSpot(2, 0) }); }
            catch (ArgumentException) { rejected = true; }
            Require(rejected, "关卡校验");
            round = new DifferenceRound(new[] { new DifferenceSpot(.5f, .5f, .1f) }, 1.5f);
            Require(round.Click(.5f, .64f) == 0, "宽图垂直方向与可见圆圈一致");
            Require(round.Click(.5f, .36f) == Ignored && round.Lives == 3, "圆圈内重复点击不扣血");
            round = new DifferenceRound(new[] { new DifferenceSpot(.5f, .5f, .1f) }, 1.5f);
            Require(round.Click(.5f, .66f) == Miss, "圆圈外点击判定");
            var rectangle = new DifferenceSpot(.25f, .125f, .125f, .75f, .2f);
            Require(rectangle.x == .3125f && rectangle.y == .5f, "矩形中心用于提示定位");
            foreach (var x in new[] { .25f, .375f })
            foreach (var y in new[] { .125f, .875f })
            {
                round = new DifferenceRound(new[] { rectangle, points[1] }, 1.5f);
                Require(round.Click(x, y) == 0, "矩形四角含边界命中");
                Require(round.Click(x, y) == Ignored && round.Count == 1 && round.Lives == 3, "矩形重复点击不扣血");
            }
            round = new DifferenceRound(new[] { rectangle }, 1.5f);
            Require(round.Click(.24f, .5f) == Miss && round.Lives == 2, "长矩形框外不按半径误命中");
            rectangle = new DifferenceSpot(384f / 1500, 864f / 1000, 278f / 1500, 136f / 1000);
            round = new DifferenceRound(new[] { rectangle }, 1.5f);
            Require(round.Click(rectangle.x, 1) == 0, "第4关 D12 归一化浮点误差不应拒绝底边矩形");
            round = new DifferenceRound(new[] { new DifferenceSpot(1296f / 1500, 864f / 1000, 204f / 1500, 136f / 1000) }, 1.5f);
            Require(round.Click(1, 1) == 0, "正好贴右下边界的矩形可命中");
            round = new DifferenceRound(new[] { new DifferenceSpot(.864f, .864f, .1360001f, .1360001f) }, 1.5f);
            Require(round.Click(1, 1) == 0, "不同运行时都容忍右下边界的单精度舍入误差");
            foreach (var invalid in new[] {
                new DifferenceSpot(.25f, .25f, 0, .1f), new DifferenceSpot(.25f, .25f, .1f, -.1f),
                new DifferenceSpot(-.01f, .25f, .1f, .1f), new DifferenceSpot(.25f, -.01f, .1f, .1f),
                new DifferenceSpot(.95f, .25f, .1f, .1f), new DifferenceSpot(.25f, .95f, .1f, .1f),
                new DifferenceSpot(.864f, .864f, .13601f, .136f), new DifferenceSpot(.864f, .864f, .136f, .13601f),
                new DifferenceSpot(float.NaN, .25f, .1f, .1f), new DifferenceSpot(.25f, .25f, .1f, float.PositiveInfinity) })
            {
                rejected = false;
                try { new DifferenceRound(new[] { invalid }); }
                catch (ArgumentException) { rejected = true; }
                Require(rejected, "拒绝空、负尺寸、越界或非有限数值矩形");
            }
            round = new DifferenceRound(points, foundMask: 1, lives: 0);
            Require(round.Count == 1 && round.IsFound(0) && round.FoundMask == 1 && round.Finished, "恢复失败前的进度");
            Require(round.Revive() && round.Lives == 3 && round.Count == 1 && round.FoundMask == 1, "复活保留已找到的位置");
            Require(!round.Revive() && round.Hint() == 1 && round.FoundMask == 1 && round.Count == 1, "复活后的提示保留进度");
            Require(round.Click(.7f, .8f) == 1 && round.Complete && !round.Revive(), "不能复活已完成关卡");
            round = new DifferenceRound(points, foundMask: 3, lives: 0);
            Require(round.Complete && !round.Revive(), "恢复已完成关卡");
            foreach (var invalidMask in new[] { -1, 4 })
            {
                rejected = false;
                try { new DifferenceRound(points, foundMask: invalidMask); }
                catch (ArgumentException) { rejected = true; }
                Require(rejected, "拒绝无效的差异点位掩码");
            }
            foreach (var total in new[] { 25, MaxSpots })
            {
                var many = new DifferenceSpot[total];
                for (var i = 0; i < total; i++) many[i] = new DifferenceSpot((i % 7 + .5f) / 7, (i / 7 + .5f) / 5, .03f);
                round = new DifferenceRound(many);
                for (var i = 0; i < total - 1; i++) Require(round.Click(many[i].x, many[i].y) == i, "多差异关卡逐个命中");
                round = new DifferenceRound(many, foundMask: round.FoundMask, lives: 2);
                Require(round.Count == total - 1 && round.Hint() == total - 1 && round.Lives == 2, "多差异关卡恢复未完成进度");
                Require(round.Click(many[total - 1].x, many[total - 1].y) == total - 1 && round.Complete &&
                    round.FoundMask == (int)((1L << total) - 1), "多差异关卡最后一位命中并完成");
                round = new DifferenceRound(many, foundMask: round.FoundMask);
                Require(round.Complete && round.Count == total && round.IsFound(total - 1), "多差异关卡完整位掩码恢复");
                round = new DifferenceRound(many, foundMask: 1 << (total - 1));
                Require(round.Count == 1 && round.IsFound(total - 1) && !round.IsFound(0) && round.Hint() == 0, "最高差异位独立恢复");
                foreach (var invalidMask in new[] { -1, int.MinValue })
                {
                    rejected = false;
                    try { new DifferenceRound(many, foundMask: invalidMask); }
                    catch (ArgumentException) { rejected = true; }
                    Require(rejected, "多差异关卡拒绝负位掩码");
                }
            }
            rejected = false;
            try { new DifferenceRound(new DifferenceSpot[MaxSpots + 1]); }
            catch (ArgumentException) { rejected = true; }
            Require(rejected, "拒绝超出正整数位掩码容量的关卡");
            foreach (var invalidLives in new[] { -1, 4 })
            {
                rejected = false;
                try { new DifferenceRound(points, lives: invalidLives); }
                catch (ArgumentException) { rejected = true; }
                Require(rejected, "拒绝无效的剩余机会");
            }
        }

        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("找不同检查失败：" + message); }
    }
}
