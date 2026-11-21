using System;
using System.Collections.Generic;
using UnityEngine;
using static Carousel.Shapes;

namespace Carousel
{
    public sealed partial class Stage
    {
        readonly Dictionary<int, Transform> plates = new Dictionary<int, Transform>();
        Transform glove, cuff, wrist;
        Fighter gloveOwner;
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
                Test(f.Head.position + f.Head.forward * .035f, V(.218f * f.Head.localScale.x, .265f, .26f), f.Head.forward);
                Test(f.Torso.position + Vector3.up * .67f, V(.56f, .51f, .285f), f.Root.forward);
                void Test(Vector3 center, Vector3 radii, Vector3 forward)
                {
                    var delta = eye - center;
                    float length = (float)Math.Sqrt(forward.x * forward.x + forward.z * forward.z);
                    float x = forward.x / length, z = forward.z / length;
                    Vector3 ToLocal(Vector3 p) =>
                        V((p.x * z - p.z * x) / radii.x, p.y / radii.y, (p.x * x + p.z * z) / radii.z);
                    Vector3 o = ToLocal(delta), d = ToLocal(ray);
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

        void PositionProp(Vector3 rest, Vector3 direction, Fighter holder)
        {
            bool holding = viewSeat != 0 && (Time.time < step ? propActor : active) == viewSeat;
            Transform anchor = holding ? Eye.transform : transform;
            if (prop.parent != anchor)
                prop.SetParent(anchor, true);
            if (holder != gloveOwner)
            {
                gloveOwner?.HoldProp(false);
                gloveOwner = holder;
                if (holder != null)
                {
                    var model = ModelLibrary.Load("Revolver");
                    model.Repaint("hand", glove, holder.Palette);
                    model.Repaint("cuff", cuff, holder.Palette);
                }
            }
            cuff.gameObject.SetActive(holding);
            if (holding)
            {
                glove.gameObject.SetActive(true);
                float phase = Math.Max(0, (spinUntil - Time.time) / .56f);
                float tilt = Mathf.Sin(Mathf.PI * phase);
                // Anchor the display pose in screen space across the supported vertical FOV range.
                float framing = (float)(Math.Tan(Eye.fieldOfView * Math.PI / 360) / Math.Tan(72 * Math.PI / 360));
                prop.localPosition = V(.26f * framing, -.145f * framing + .035f * tilt, .71f - .045f * recoil);
                prop.localRotation = Quaternion.Euler(-4 - 13 * recoil - 9 * tilt, -26, -8 - 22 * tilt);
                return;
            }

            if (direction.sqrMagnitude > .001f)
                prop.rotation = Quaternion.Slerp(prop.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 12);
            Vector3 target = holder == null ? rest : rest - (wrist.position - prop.position);
            prop.position = Vector3.Lerp(prop.position, target - prop.forward * (.08f * recoil), Mathf.Min(1, Time.deltaTime * 13));
            bool inHand = holder != null && (wrist.position - rest).sqrMagnitude < .012f;
            glove.gameObject.SetActive(inHand);
            holder?.HoldProp(inHand);
        }

        Transform MakeProp()
        {
            var root = Group(transform, "Револьвер", V(0, 1.34f, 0));
            var model = ModelLibrary.Load("Revolver");
            model.Attach("frame", root);
            var axis = V(0, .036f, 0);
            rotor = Group(root, "Барабан", axis);
            model.Attach("cylinder", rotor).localPosition -= axis;
            wrist = Group(root, "Запястье", V(.062f, -.193f, -.175f));
            glove = model.Attach("hand", root);
            cuff = model.Attach("cuff", root);
            cuff.gameObject.SetActive(false);
            glove.gameObject.SetActive(false);
            return root;
        }
    }
}
