#import <UIKit/UIKit.h>

extern "C" void DifferencePlayHaptic(int style) {
    if (@available(iOS 10.0, *)) {
        static UIImpactFeedbackGenerator *light;
        static UIImpactFeedbackGenerator *medium;
        static UIImpactFeedbackGenerator *heavy;
        if (!light) {
            light = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
            medium = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium];
            heavy = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy];
        }
        UIImpactFeedbackGenerator *generator = style >= 2 ? heavy : style == 1 ? medium : light;
        [generator prepare];
        [generator impactOccurred];
    }
}
