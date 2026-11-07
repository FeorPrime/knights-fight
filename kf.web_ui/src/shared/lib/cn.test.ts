import { describe, expect, it } from 'vitest';
import { cn } from './cn';

describe('cn', () => {
    it('joins class names', () => {
        expect(cn('a', 'b')).toBe('a b');
    });

    it('drops falsy values', () => {
        const isActive = false;
        expect(cn('a', isActive && 'b', undefined, null, 'c')).toBe('a c');
    });
});
