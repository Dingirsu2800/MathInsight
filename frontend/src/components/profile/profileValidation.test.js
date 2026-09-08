import { describe, expect, it } from 'vitest';
import {
  buildDisplayName,
  PROFILE_PHONE_PATTERN,
  validateProfileUpdate,
} from './UserProfileForm';

describe('profile update validation', () => {
  it('accepts a valid Vietnamese mobile number and a valid date', () => {
    const result = validateProfileUpdate({
      firstName: 'Chu',
      lastName: 'Khuê',
      phoneNumber: '0901234567',
      dateOfBirth: '2007-04-15',
      roleName: 'Student',
      student: { currentGrade: 12 },
    });

    expect(result).toEqual({});
  });

  it('rejects wrong mobile prefixes and wrong lengths', () => {
    expect(validateProfileUpdate({ phoneNumber: '0101234567' })).toMatchObject({
      phoneNumber: 'Số điện thoại không hợp lệ.',
    });

    expect(validateProfileUpdate({ phoneNumber: '090123456' })).toMatchObject({
      phoneNumber: 'Số điện thoại không hợp lệ.',
    });

    expect(validateProfileUpdate({ phoneNumber: '090abc4567' })).toMatchObject({
      phoneNumber: 'Số điện thoại không hợp lệ.',
    });
  });

  it('rejects future DOB and implausible age for THPT students', () => {
    const futureDob = validateProfileUpdate({
      dateOfBirth: '2099-01-01',
      roleName: 'Student',
      student: { currentGrade: 12 },
    });
    expect(futureDob).toMatchObject({
      dateOfBirth: 'Ngày sinh không được lớn hơn ngày hiện tại.',
    });

    const youngAge = validateProfileUpdate({
      dateOfBirth: '2014-01-01',
      roleName: 'Student',
      student: { currentGrade: 12 },
    });
    expect(youngAge).toMatchObject({
      dateOfBirth: 'Ngày sinh không phù hợp với độ tuổi học sinh THPT.',
    });
  });
});

describe('display name formatting', () => {
  it('keeps the last name then first name ordering and trims whitespace', () => {
    expect(buildDisplayName('Chu', 'Khuê')).toBe('Chu Khuê');
    expect(buildDisplayName('  Chu  ', '   Khuê  ')).toBe('Chu Khuê');
    expect(buildDisplayName('', 'Khuê')).toBe('Khuê');
  });

  it('uses the same format for the shared phone pattern', () => {
    expect(PROFILE_PHONE_PATTERN.test('0901234567')).toBe(true);
    expect(PROFILE_PHONE_PATTERN.test('0301234567')).toBe(true);
    expect(PROFILE_PHONE_PATTERN.test('0101234567')).toBe(false);
  });
});
