using System;
using System.Collections.Generic;
using UnityEngine;
using static Carousel.Shapes;

namespace Carousel
{
    public sealed partial class Stage
    {
        readonly Dictionary<int, Transform> plates = new Dictionary<int, Transform>();
        Transform glove;
        int viewSeat;
        float yaw, pitch;
        public int FieldOfView = 72;
        public bool Turning => Time.time < spinUntil;
        public bool Acting => Time.time < step;

        public void ViewAs(int id)
        {
            if (viewSeat == id)
                return;
            if (cast.TryGetValue(viewSeat, out var old))
                old.Root.gameObject.SetActive(true);
            viewSeat = id;
            if (!cast.TryGetValue(id, out var player))
                return;
            player.Root.gameObject.SetActive(false);
            Vector3 heading = player.Root.forward;
            float farthest = 0;
            foreach (var entry in cast)
            {
                if (entry.Key == id || entry.Value.Fallen) continue;
                var toSeat = entry.Value.Head.position - player.Head.position;
                if (toSeat.sqrMagnitude > farthest)
                {
                    farthest = toSeat.sqrMagnitude;
                    heading = toSeat;
                }
            }
            yaw = (float)(Math.Atan2(heading.x, heading.z) * 180 / Math.PI);
            pitch = 4;
            Look(0, 0);
        }

        public void Look(float horizontal, float vertical)
        {
            if (!cast.TryGetValue(viewSeat, out var player))
                return;
            yaw = (yaw + horizontal) % 360;
            pitch = Math.Max(-76, Math.Min(76, pitch + vertical));
            Eye.transform.position = player.Head.position + Vector3.up * .045f;
            Eye.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            Eye.fieldOfView = FieldOfView;
        }

        // Intersect the central view ray with each visible avatar's head and upper body.
        // The first surface along the ray owns the click when seats overlap in projection.
        public int AimedSeat()
        {
            Vector3 ray = Eye.transform.forward, eye = Eye.transform.position;
            int target = 0;
            float nearest = float.PositiveInfinity;
            foreach (var entry in cast)
            {
                var f = entry.Value;
                if (entry.Key == viewSeat || f.Fallen)
                    continue;
                Test(f.Head.position, V(.32f, .34f, .32f));
                Test(f.Torso.position + Vector3.up * .65f, V(.62f, .54f, .51f));
                void Test(Vector3 center, Vector3 radii)
                {
                    var delta = eye - center;
                    Vector3 o = V(delta.x / radii.x, delta.y / radii.y, delta.z / radii.z);
                    Vector3 d = V(ray.x / radii.x, ray.y / radii.y, ray.z / radii.z);
                    float dot = o.x * d.x + o.y * d.y + o.z * d.z;
                    float discriminant = dot * dot - d.sqrMagnitude * (o.sqrMagnitude - 1);
                    if (discriminant < 0)
                        return;
                    float distance = (-dot - (float)Math.Sqrt(discriminant)) / d.sqrMagnitude;
                    if (distance > 0 && distance < nearest)
                    {
                        nearest = distance;
                        target = entry.Key;
                    }
                }
            }

            return target;
        }

        public void Twirl()
        {
            spinUntil = Time.time + .56f;
            speaker.PlayOneShot(whirr, .55f);
        }

        void PositionProp(Vector3 rest, Vector3 direction)
        {
            bool holding = viewSeat != 0 && (Time.time < step ? propActor : active) == viewSeat;
            Transform anchor = holding ? Eye.transform : transform;
            if (prop.parent != anchor)
                prop.SetParent(anchor, true);
            glove.gameObject.SetActive(holding);
            if (holding)
            {
                float phase = Math.Max(0, (spinUntil - Time.time) / .56f);
                float tilt = Mathf.Sin(Mathf.PI * phase);
                // Anchor the display pose in screen space across the supported vertical FOV range.
                float framing = (float)(Math.Tan(Eye.fieldOfView * Math.PI / 360) / Math.Tan(72 * Math.PI / 360));
                prop.localPosition = V(.24f * framing, -.145f * framing + .035f * tilt, .64f - .045f * recoil);
                prop.localRotation = Quaternion.Euler(-4 - 13 * recoil - 9 * tilt, -19, -8 - 22 * tilt);
                return;
            }

            prop.position = Vector3.Lerp(prop.position, rest - prop.forward * (.13f * recoil), Mathf.Min(1, Time.deltaTime * 13));
            if (direction.sqrMagnitude > .001f)
                prop.rotation = Quaternion.Slerp(prop.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 12);
        }

        void FinishProp(Transform t)
        {
            const int navy = 0x344650, brass = 0xBD924F, enamel = 0x317A76, light = 0xF9D884;
            Rod(t, V(0, .067f, .10f), V(0, .067f, .36f), .061f, navy);
            Rod(t, V(0, .067f, .34f), V(0, .067f, .374f), .065f, brass);
            Rod(t, V(0, .067f, .373f), V(0, .067f, .38f), .040f, 0x273439);
            for (int s = -1; s <= 1; s += 2)
            {
                Box(t, V(.056f * s, .068f, .219f), V(.014f, .043f, .193f), enamel);
                Box(t, V(.063f * s, .073f, .219f), V(.006f, .009f, .154f), light);
                var panel = Ball(t, V(.047f * s, -.103f, -.092f), V(.022f, .164f, .104f), enamel);
                panel.localRotation = Quaternion.Euler(16, 0, 0);
                for (int j = 0; j < 3; j++)
                    Ball(t, V(.058f * s, -.071f - .033f * j, -.09f), V(.012f, .01f, .057f), brass);
            }

            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3;
                Ball(rotor, V(.064f * Mathf.Sin(a), .064f * Mathf.Cos(a), -.073f), V(.033f, .033f, .012f), i % 2 == 0 ? brass : enamel);
                Box(rotor, V(.075f * Mathf.Sin(a), .075f * Mathf.Cos(a), -.018f), V(.024f, .021f, .061f), navy);
            }

            Ball(t, V(0, .065f, -.08f), V(.157f, .092f, .083f), navy);
            // The enamel medallion and alternating inlays remain visible while the drum turns.
            for (int s = -1; s <= 1; s += 2)
            {
                Ball(t, V(.065f * s, .085f, -.095f), V(.018f, .045f, .043f), brass);
                Ball(t, V(.075f * s, .085f, -.095f), V(.008f, .026f, .027f), enamel);
            }

            Rod(t, V(0, .035f, -.097f), V(0, .035f, -.075f), .033f, brass);
            glove = Group(t, "Рука игрока", Vector3.zero);
            var wrist = Ball(glove, V(.071f, -.192f, -.175f), V(.135f, .135f, .27f), 0xBD805B);
            wrist.localRotation = Quaternion.Euler(-27, -22, 0);
            Ball(glove, V(.053f, -.137f, -.117f), V(.145f, .155f, .167f), 0x493C35);
            Ball(glove, V(.09f, -.223f, -.238f), V(.147f, .14f, .061f), enamel);
            for (int i = 0; i < 3; i++)
            {
                var finger = Ball(glove, V(-.012f, -.097f - i * .032f, -.035f - i * .011f), V(.113f, .039f, .056f), 0x655043);
                finger.Rotate(0, 14, 5);
            }

            var thumb = Ball(glove, V(.062f, -.065f, -.10f), V(.068f, .061f, .136f), 0x79614E);
            thumb.Rotate(16, -32, 0);
            glove.gameObject.SetActive(false);
        }
    }
}
