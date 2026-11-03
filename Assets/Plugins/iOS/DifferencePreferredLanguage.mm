#import <Foundation/Foundation.h>
#include <string.h>

extern "C" const char *DifferencePreferredLanguage(void)
{
    static char language[256];
    NSString *preferred = [NSLocale preferredLanguages].firstObject;
    const char *value = preferred.UTF8String;
    if (!value) return nullptr;
    strlcpy(language, value, sizeof(language));
    return language;
}
