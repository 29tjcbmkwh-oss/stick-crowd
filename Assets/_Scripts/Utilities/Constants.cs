namespace _Scripts.Utilities
{
    public static class Constants
    {
        // Forward speed is expressed in world units per second because movement is
        // multiplied by Time.deltaTime. The inherited 0.09 value made the 123-unit
        // level take more than 20 minutes on device; three units/second keeps a run
        // in the intended hyper-casual 40-second range without changing its layout.
        public static float SPEED_COEFFICIENT = 3F;
        public static float SENSITIVITY_COEFFICIENT = 0.07F;
        
        // CLamp the translate on x
        public static float CLAMP_MODIFIER = 2.2F;
        public static float MAX_SCORE = 100F;
        
        
        public static float LERP_COEF = 0.3F;
        public static float SWERVE_SPEED = 1250F;
        
        public enum CorridorTypes
        {
            Increase,
            Decrease,
            Multiply,
            Divide
        }
        
    }
}
