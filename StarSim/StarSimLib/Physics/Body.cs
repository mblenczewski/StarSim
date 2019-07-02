﻿using System;
using System.Collections.Generic;
using StarSimLib.Data_Structures;

namespace StarSimLib.Physics
{
    /// <summary>
    /// Represents a stellar body.
    /// </summary>
    public class Body
    {
        /// <summary>
        /// Describes how the <see cref="Body"/> instance should be formatted as a <see cref="string"/>.
        /// </summary>
        private const string BodyFormatString =
            "Body {0,2}.{1,-4}: Pos-{2}, Vel-{3} Mass-{4,3}";

        /// <summary>
        /// Sample rate for the previous position. Used to improve performance and get a longer orbit tracer tail
        /// for less computation. The previous position will be saved once every 20 sampling opportunities.
        /// </summary>
        private const int PositionSampleRate = 20;

        /// <summary>
        /// Backing field for the <see cref="PreviousPositions"/> property.
        /// </summary>
        private readonly Queue<Vector4d> previousPositions;

        /// <summary>
        /// The backing field for the <see cref="Force"/> property.
        /// </summary>
        private Vector4d force;

        /// <summary>
        /// Backing field for the <see cref="Mass"/> property.
        /// </summary>
        private double mass;

        /// <summary>
        /// Backing field for the <see cref="Position"/> property.
        /// </summary>
        private Vector4d position;

        /// <summary>
        /// Counts the number of sampling opportunities that have gone by since the last position sample. Resets once
        /// it reaches the value of <see cref="PositionSampleRate"/>.
        /// </summary>
        private int positionSampleCounter = 0;

        /// <summary>
        /// Backing field for the <see cref="Velocity"/> property.
        /// </summary>
        private Vector4d velocity;

        /// <summary>
        /// The generation that this body belongs to.
        /// </summary>
        public readonly uint Generation;

        /// <summary>
        /// The unique id for this body.
        /// </summary>
        public readonly uint Id;

        /// <summary>
        /// Initialises a new instance of the <see cref="Body"/> class.
        /// </summary>
        /// <param name="position">The starting position of the <see cref="Body"/>.</param>
        /// <param name="velocity">The starting velocity of the <see cref="Body"/>.</param>
        /// <param name="mass">The starting mass of the <see cref="Body"/>.</param>
        /// <param name="generation">The generation that this <see cref="Body"/> belongs to.</param>
        /// <param name="id">The unique id for this body.</param>
        public Body(Vector4d position, Vector4d velocity, double mass, uint generation = 1, uint id = 1)
        {
            Generation = generation;
            Id = id;

            this.position = position;
            this.position.W = 1;
            this.velocity = velocity;
            this.mass = mass;

            force = new Vector4d();
            previousPositions = new Queue<Vector4d>(Constants.StoredPreviousPositionCount + 1);
        }

        /// <summary>
        /// The current force on the <see cref="Body"/> in 3D space.
        /// </summary>
        public Vector4d Force
        {
            get { return force; }
        }

        /// <summary>
        /// The mass of the <see cref="Body"/>.
        /// </summary>
        public double Mass
        {
            get { return mass; }
        }

        /// <summary>
        /// The current position of the <see cref="Body"/> in 3D space.
        /// </summary>
        public Vector4d Position
        {
            get { return position; }
        }

        /// <summary>
        /// A <see cref="Queue{T}"/> containing previous positions of the body.
        /// </summary>
        public ref readonly Queue<Vector4d> PreviousPositions
        {
            get { return ref previousPositions; }
        }

        /// <summary>
        /// Whether to record previous positions of this instance, to render an orbit tracer behind it.
        /// </summary>
        public bool RecordPreviousPositions { get; set; }

        /// <summary>
        /// The current velocity of the <see cref="Body"/> in 3D space.
        /// </summary>
        public Vector4d Velocity
        {
            get { return velocity; }
        }

        /// <summary>
        /// Enqueues the current position on the <see cref="previousPositions"/> queue, to save it.
        /// Will dequeue positions from the queue if the number of stored positions exceeds the
        /// value in <see cref="Constants.StoredPreviousPositionCount"/>. Will only enqueue the
        /// current position if the <see cref="PositionSampleRate"/> is met.
        /// </summary>
        private void EnqueuePosition()
        {
            // if the sample rate limit has not yet been met, don't sample a position
            if (++positionSampleCounter < PositionSampleRate) return;

            previousPositions.Enqueue(position);

            if (previousPositions.Count > Constants.StoredPreviousPositionCount)
            {
                previousPositions.Dequeue();
            }

            // reset the counter
            positionSampleCounter = 0;
        }

        /// <summary>
        /// Gets the force vector for the attraction between <see cref="Body"/> A and <see cref="Body"/> B.
        /// </summary>
        /// <param name="a">The first <see cref="Body"/> instance.</param>
        /// <param name="b">The second <see cref="Body"/> instance.</param>
        /// <returns></returns>
        public static Vector4d GetForceBetween(Body a, Body b)
        {
            // Inlines the Body.DistanceTo(Body) as the position deltas need to be cached for later,
            // as well as to gain a small performance increase
            double dx = b.Position.X - a.Position.X,
                   dy = b.Position.Y - a.Position.Y,
                   dz = b.Position.Z - a.Position.Z;

            // The distance between two bodies can be found via taking the magnitude of their displacements,
            // as shown here via pythagoras
            double distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            double numerator = Constants.G * a.Mass * b.Mass;
            double denominator = distance * distance + Constants.SofteningFactor2;

            // Using the equation Force = Gravitational Constant * Mass(a) * Mass(b) / distance(a, b)^2
            // with a softening factor, we get the attraction force vector between the 2 bodies
            double force = numerator / denominator;

            return new Vector4d(force * dx / distance, force * dy / distance, force * dz / distance);
        }

        /// <summary>
        /// Calculates the force between this instance and the given other body, and adds the resultant vector to the
        /// internal total force vector.
        /// </summary>
        /// <param name="otherBody">The other body to calculate the force between.</param>
        public void AddForce(Body otherBody)
        {
            force += GetForceBetween(this, otherBody);
        }

        /// <summary>
        /// Clears the <see cref="Queue{T}"/> holding the previous positions.
        /// </summary>
        public void ClearPreviousPositionQueue()
        {
            previousPositions.Clear();
        }

        /// <summary>
        /// Collides this instance with the given <see cref="Body"/> instance.
        /// </summary>
        /// <param name="otherBody">The other instance with which to collide.</param>
        public void Collide(Body otherBody)
        {
            mass += otherBody.Mass;
            velocity += otherBody.Velocity;
        }

        /// <summary>
        /// Finds and returns the distance between the current <see cref="Body"/> instance and the given <see cref="Body"/>.
        /// In other words, it finds the magnitude of the translation vector between the two <see cref="Body"/> instances.
        /// </summary>
        /// <param name="body">The other <see cref="Body"/> instance to which to calculate the distance.</param>
        /// <param name="displacement">The vector showing the displacement of the current body from the given one.</param>
        /// <returns>The distance to the other <see cref="Body"/> as a <see cref="float"/>.</returns>
        public double DistanceTo(Body body, out Vector4d displacement)
        {
            double dpx = body.Position.X - Position.X,
                   dpy = body.Position.Y - Position.Y,
                   dpz = body.Position.Z - Position.Z;

            displacement = new Vector4d(dpx, dpy, dpz);

            return Math.Sqrt(dpx * dpx + dpy * dpy + dpz * dpz);
        }

        /// <summary>
        /// Resets the internal force vector.
        /// </summary>
        public void ResetForce()
        {
            force.X = 0;
            force.Y = 0;
            force.Z = 0;
        }

        /// <summary>
        /// Updates the <see cref="Position"/> of the current body using the internal force vector and the given time step.
        /// </summary>
        /// <param name="deltaTime">The time step.</param>
        public void Update(double deltaTime)
        {
            velocity += deltaTime * force / mass;

            if (RecordPreviousPositions)
            {
                EnqueuePosition();
            }

            position += deltaTime * velocity;
        }

        /// <summary>
        /// Updates the <see cref="Position"/> of the current body using the given force vector and time step.
        /// </summary>
        /// <param name="forceVector">The sum vector of all forces due to other <see cref="Body"/>s.</param>
        /// <param name="deltaTime">The time step.</param>
        public void Update(Vector4d forceVector, double deltaTime)
        {
            velocity += deltaTime * forceVector / mass;

            if (RecordPreviousPositions)
            {
                EnqueuePosition();
            }

            position += deltaTime * velocity;
        }

        #region Overrides of Object

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Format(BodyFormatString, Generation, Id, Position, Velocity, Mass);
        }

        #endregion Overrides of Object
    }
}