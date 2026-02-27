import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { AuthService } from './auth.service';
import { environment } from '../../environments/environment';

const base64UrlEncode = (value: string): string =>
  btoa(value).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '');

const createToken = (expiresInSeconds: number): string => {
  const header = base64UrlEncode(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const exp = Math.floor(Date.now() / 1000) + expiresInSeconds;
  const payload = base64UrlEncode(JSON.stringify({ exp }));
  return `${header}.${payload}.sig`;
};

describe('AuthService refreshTokenIfNeeded', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [AuthService]
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
    localStorage.clear();
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('refreshes when token expires within the buffer', done => {
    const token = createToken(60);
    localStorage.setItem('auth_token', token);

    service.refreshTokenIfNeeded(300).subscribe(res => {
      expect(res?.token).toBe('new-token');
      expect(localStorage.getItem('auth_token')).toBe('new-token');
      done();
    });

    const req = httpMock.expectOne(`${environment.apiUrl}/auth/refresh`);
    expect(req.request.method).toBe('POST');
    req.flush({
      token: 'new-token',
      user: { id: '1', email: 'a@b.com', name: 'Test' }
    });
  });

  it('skips refresh when token expiry is outside the buffer', done => {
    const token = createToken(600);
    localStorage.setItem('auth_token', token);

    service.refreshTokenIfNeeded(300).subscribe(res => {
      expect(res).toBeNull();
      done();
    });
  });
});
